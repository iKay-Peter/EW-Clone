'use strict';

var MAX_EVENTS = 300;
var counts = {};
var hooked = {};
var GetFileType = null;

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
  var n = Math.min(length, 512);
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

function isPipe(handle) {
  if (!GetFileType) return false;
  try {
    return GetFileType(handle) === 3;
  } catch (e) {
    return false;
  }
}

function installNtHooks() {
  hook('ntdll.dll', 'NtWriteFile', {
    onEnter: function (args) {
      if (!isPipe(args[0])) return;
      var len = args[6].toInt32();
      if (bump('NtWriteFile') <= MAX_EVENTS) {
        log('NT_PIPE_WRITE', 'handle=' + args[0], preview(args[5], len));
      }
    }
  });

  hook('ntdll.dll', 'NtReadFile', {
    onEnter: function (args) {
      this.handle = args[0];
      this.iosb = args[4];
      this.buffer = args[5];
      this.length = args[6].toInt32();
    },
    onLeave: function (retval) {
      if (!isPipe(this.handle)) return;
      var got = 0;
      try { got = this.iosb.add(4).readU32(); } catch (e) {}
      if (bump('NtReadFile') <= MAX_EVENTS) {
        log('NT_PIPE_READ', 'handle=' + this.handle, 'status=' + retval.toInt32(), preview(this.buffer, got));
      }
    }
  });

  hook('ntdll.dll', 'NtFsControlFile', {
    onEnter: function (args) {
      if (!isPipe(args[0])) return;
      this.handle = args[0];
      this.iosb = args[4];
      this.code = args[5].toInt32();
      this.inBuffer = args[6];
      this.inLength = args[7].toInt32();
      this.outBuffer = args[8];
      this.outLength = args[9].toInt32();
      if (bump('NtFsControlFile') <= MAX_EVENTS) {
        log('NT_PIPE_FSCTL', 'handle=' + args[0], 'code=0x' + this.code.toString(16), 'in=' + preview(this.inBuffer, this.inLength));
      }
    },
    onLeave: function (retval) {
      if (!isPipe(this.handle)) return;
      var got = 0;
      try { got = this.iosb.add(4).readU32(); } catch (e) {}
      if (bump('NtFsControlFile_leave') <= MAX_EVENTS) {
        log('NT_PIPE_FSCTL_OUT', 'status=' + retval.toInt32(), 'outLen=' + got, 'out=' + preview(this.outBuffer, got));
      }
    }
  });

  hook('ntdll.dll', 'NtDeviceIoControlFile', {
    onEnter: function (args) {
      if (!isPipe(args[0])) return;
      this.handle = args[0];
      this.iosb = args[4];
      this.code = args[5].toInt32();
      this.inBuffer = args[6];
      this.inLength = args[7].toInt32();
      this.outBuffer = args[8];
      this.outLength = args[9].toInt32();
      if (bump('NtDeviceIoControlFile') <= MAX_EVENTS) {
        log('NT_PIPE_IOCTL', 'handle=' + args[0], 'code=0x' + this.code.toString(16), 'in=' + preview(this.inBuffer, this.inLength));
      }
    },
    onLeave: function (retval) {
      if (!isPipe(this.handle)) return;
      var got = 0;
      try { got = this.iosb.add(4).readU32(); } catch (e) {}
      if (bump('NtDeviceIoControlFile_leave') <= MAX_EVENTS) {
        log('NT_PIPE_IOCTL_OUT', 'status=' + retval.toInt32(), 'outLen=' + got, 'out=' + preview(this.outBuffer, got));
      }
    }
  });
}

try {
  var getFileTypeAddr = findExport('kernel32.dll', 'GetFileType');
  if (getFileTypeAddr !== null) {
    GetFileType = new NativeFunction(getFileTypeAddr, 'uint32', ['pointer']);
    log('GETFILETYPE_OK');
  }
} catch (e) {
  log('GETFILETYPE_ERROR', e && e.message ? e.message : String(e));
}

installNtHooks();
log('NT_PIPE_PROBE_READY', 'pid=' + Process.id, 'arch=' + Process.arch);
