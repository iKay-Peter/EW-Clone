'use strict';

var MAX_EVENTS = 500;
var pipeMap = {};
var counts = {};
var hooked = {};

function log() {
  console.log(Array.prototype.slice.call(arguments).join(' '));
}

function bump(key) {
  counts[key] = (counts[key] || 0) + 1;
  return counts[key];
}

function findExport(moduleName, exportName) {
  try {
    var m = Process.getModuleByName(moduleName);
    if (m && m.getExportByName) return m.getExportByName(exportName);
    if (m && m.findExportByName) return m.findExportByName(exportName);
  } catch (e) {}
  try {
    if (Module.getGlobalExportByName) return Module.getGlobalExportByName(exportName);
  } catch (e) {}
  return null;
}

function hook(moduleName, exportName, handlers) {
  var key = moduleName.toLowerCase() + '!' + exportName;
  if (hooked[key]) return;
  var addr = findExport(moduleName, exportName);
  if (addr === null) {
    log('HOOK_MISS', moduleName + '!' + exportName);
    return;
  }
  try {
    Interceptor.attach(addr, handlers);
    hooked[key] = true;
    log('HOOK', moduleName + '!' + exportName, addr);
  } catch (e) {
    log('HOOK_ERROR', moduleName + '!' + exportName, e && e.message ? e.message : String(e));
  }
}

function preview(buffer, length) {
  if (!buffer || length <= 0) return 'empty';
  var n = Math.min(length, 768);
  var bytes;
  try {
    bytes = new Uint8Array(buffer.readByteArray(n));
  } catch (e) {
    return 'read-error';
  }
  var hexParts = [];
  var ascii = '';
  for (var i = 0; i < bytes.length; i++) {
    var b = bytes[i];
    hexParts.push(('0' + b.toString(16)).slice(-2));
    ascii += (b >= 32 && b <= 126) ? String.fromCharCode(b) : '.';
  }
  return 'len=' + length + ' hex=' + hexParts.join('') + ' ascii=' + ascii;
}

function readName(ptrName) {
  if (!ptrName || ptrName.isNull()) return '';
  try { return ptrName.readUtf16String(); } catch (e) { return ''; }
}

function isEzw(name) {
  return !!name && name.toUpperCase().indexOf('EZW_') !== -1;
}

function markHandle(handle, name, kind) {
  if (!handle || handle.isNull()) return;
  var i = handle.toInt32();
  if (i === -1 || i === 0) return;
  var k = handle.toString();
  var existing = pipeMap[k];
  if (existing && existing.name === name) return;
  pipeMap[k] = { name: name, kind: kind };
  log('EZW_HANDLE', kind, 'handle=' + handle, 'path=' + name);
}

function getPipe(handle) {
  if (!handle) return null;
  return pipeMap[handle.toString()] || null;
}

function installHandleHooks() {
  var modules = ['kernelbase.dll', 'kernel32.dll'];
  modules.forEach(function (mod) {
    hook(mod, 'CreateFileW', {
      onEnter: function (args) {
        this.name = readName(args[0]);
      },
      onLeave: function (retval) {
        if (isEzw(this.name)) markHandle(retval, this.name, 'OPEN');
      }
    });

    hook(mod, 'CreateNamedPipeW', {
      onEnter: function (args) {
        this.name = readName(args[0]);
      },
      onLeave: function (retval) {
        if (isEzw(this.name)) markHandle(retval, this.name, 'CREATE');
      }
    });
  });
}

function installNtHooks() {
  hook('ntdll.dll', 'NtWriteFile', {
    onEnter: function (args) {
      var p = getPipe(args[0]);
      if (!p) return;
      var len = args[6].toInt32();
      if (bump('NtWriteFile') <= MAX_EVENTS) {
        log('EZW_WRITE', 'handle=' + args[0], 'path=' + p.name, preview(args[5], len));
      }
    }
  });

  hook('ntdll.dll', 'NtReadFile', {
    onEnter: function (args) {
      var p = getPipe(args[0]);
      if (!p) return;
      this.pipe = p;
      this.handle = args[0];
      this.iosb = args[4];
      this.buffer = args[5];
      this.length = args[6].toInt32();
    },
    onLeave: function (retval) {
      if (!this.pipe) return;
      var got = 0;
      try { got = this.iosb.add(4).readU32(); } catch (e) {}
      if (bump('NtReadFile') <= MAX_EVENTS) {
        log('EZW_READ', 'handle=' + this.handle, 'path=' + this.pipe.name, 'status=' + retval.toInt32(), preview(this.buffer, got));
      }
    }
  });

  hook('ntdll.dll', 'NtFsControlFile', {
    onEnter: function (args) {
      var p = getPipe(args[0]);
      if (!p) return;
      this.pipe = p;
      this.handle = args[0];
      this.iosb = args[4];
      this.code = args[5].toInt32();
      this.inBuffer = args[6];
      this.inLength = args[7].toInt32();
      this.outBuffer = args[8];
      this.outLength = args[9].toInt32();
      if (bump('NtFsControlFile') <= MAX_EVENTS) {
        log('EZW_FSCTL', 'handle=' + args[0], 'path=' + p.name, 'code=0x' + this.code.toString(16), 'in=' + preview(this.inBuffer, this.inLength));
      }
    },
    onLeave: function (retval) {
      if (!this.pipe) return;
      var got = 0;
      try { got = this.iosb.add(4).readU32(); } catch (e) {}
      if (bump('NtFsControlFile_leave') <= MAX_EVENTS) {
        log('EZW_FSCTL_OUT', 'path=' + this.pipe.name, 'status=' + retval.toInt32(), 'outLen=' + got, 'out=' + preview(this.outBuffer, got));
      }
    }
  });
}

function installMappingHooks() {
  hook('kernelbase.dll', 'CreateFileMappingW', {
    onEnter: function (args) {
      this.name = readName(args[5]);
      this.high = args[3].toInt32();
      this.low = args[4].toInt32();
    },
    onLeave: function (retval) {
      if (this.name && (/EZW|EASYWORSHIP/i).test(this.name)) {
        log('EZW_MAP_CREATE', 'handle=' + retval, 'name=' + this.name, 'sizeHigh=' + this.high, 'sizeLow=' + this.low);
      }
    }
  });

  hook('kernelbase.dll', 'OpenFileMappingW', {
    onEnter: function (args) {
      this.name = readName(args[2]);
    },
    onLeave: function (retval) {
      if (this.name && (/EZW|EASYWORSHIP/i).test(this.name)) {
        log('EZW_MAP_OPEN', 'handle=' + retval, 'name=' + this.name);
      }
    }
  });

  hook('kernelbase.dll', 'MapViewOfFile', {
    onEnter: function (args) {
      this.map = args[0];
      this.size = args[4].toInt32();
    },
    onLeave: function (retval) {
      if (this.size > 0 || !retval.isNull()) {
        log('EZW_MAP_VIEW', 'map=' + this.map, 'base=' + retval, 'size=' + this.size);
      }
    }
  });

  hook('user32.dll', 'RegisterWindowMessageW', {
    onEnter: function (args) {
      this.name = readName(args[0]);
    },
    onLeave: function (retval) {
      if (this.name) log('EZW_REG_MSG', 'id=' + retval.toInt32(), 'name=' + this.name);
    }
  });
}

installHandleHooks();
installNtHooks();
installMappingHooks();
log('EZW_PIPE_PROBE_READY', 'pid=' + Process.id, 'arch=' + Process.arch);
