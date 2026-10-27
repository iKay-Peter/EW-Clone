'use strict';

var pipeHandle = null;
var MAX_EVENTS = 400;
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

function readWide(ptr) {
  try {
    return ptr.isNull() ? null : ptr.readUtf16String();
  } catch (e) {
    return null;
  }
}

function isPipeHandle(h) {
  try {
    return pipeHandle !== null && h.equals(pipeHandle);
  } catch (e) {
    return false;
  }
}

function preview(buffer, length) {
  var n = Math.min(length, 256);
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

function setPipeHandle(h, path) {
  if (pipeHandle !== null) return;
  try {
    pipeHandle = h;
    log('PIPE_HANDLE_SET', 'handle=' + h, 'path=' + path);
    var getFileType = findExport('kernel32.dll', 'GetFileType');
    if (getFileType !== null) {
      var GetFileType = new NativeFunction(getFileType, 'uint32', ['pointer']);
      log('PIPE_FILETYPE', GetFileType(h));
    }
  } catch (e) {
    log('PIPE_HANDLE_ERROR', e && e.message ? e.message : String(e));
  }
}

function installFileHook(moduleName) {
  hook(moduleName, 'CreateFileW', {
    onEnter: function (args) {
      this.path = readWide(args[0]);
      this.access = args[1].toInt32();
      this.creation = args[4].toInt32();
    },
    onLeave: function (retval) {
      if (!this.path) return;
      if (this.path.toLowerCase().indexOf('ezw_helper_pipe') !== -1) {
        setPipeHandle(retval, this.path);
        log('PIPE_OPEN', this.path, 'handle=' + retval, 'access=' + this.access, 'creation=' + this.creation);
      }
    }
  });

  hook(moduleName, 'CreateNamedPipeW', {
    onEnter: function (args) {
      var name = readWide(args[0]);
      if (name) log('PIPE_CREATE', name, 'openMode=' + args[1].toInt32(), 'pipeMode=' + args[2].toInt32(), 'maxInstances=' + args[3].toInt32());
    }
  });
}

function installPipeIoHook(moduleName) {
  hook(moduleName, 'WriteFile', {
    onEnter: function (args) {
      this.handle = args[0];
      if (!isPipeHandle(args[0])) return;
      var len = args[2].toInt32();
      if (bump('WriteFile') <= MAX_EVENTS) log('PIPE_WRITE', preview(args[1], len));
    }
  });

  hook(moduleName, 'ReadFile', {
    onEnter: function (args) {
      this.handle = args[0];
      this.buffer = args[1];
      this.length = args[2].toInt32();
      this.readPtr = args[3];
    },
    onLeave: function (retval) {
      if (!isPipeHandle(this.handle)) return;
      var read = 0;
      try { read = this.readPtr.readU32(); } catch (e) {}
      if (bump('ReadFile') <= MAX_EVENTS) log('PIPE_READ', 'rc=' + retval.toInt32(), preview(this.buffer, read));
    }
  });
}

function installProcessHook(moduleName) {
  hook(moduleName, 'CreateProcessW', {
    onEnter: function (args) {
      var app = readWide(args[0]);
      var cmd = readWide(args[1]);
      if (bump('CreateProcessW') <= 50) log('PROCESS_CREATE', 'app=' + app, 'cmd=' + cmd);
    }
  });
}

installFileHook('kernel32.dll');
installFileHook('kernelbase.dll');
installPipeIoHook('kernel32.dll');
installPipeIoHook('kernelbase.dll');
installProcessHook('kernel32.dll');
installProcessHook('kernelbase.dll');

log('MAIN_PIPE_PROBE_READY', 'pid=' + Process.id, 'arch=' + Process.arch);
