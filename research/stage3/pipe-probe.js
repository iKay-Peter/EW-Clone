'use strict';

var PIPE_HANDLE = ptr('0xe1c');
var MAX_EVENTS = 200;
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

function isPipeHandle(h) {
  try {
    return h.equals(PIPE_HANDLE);
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

try {
  var getFileType = findExport('kernel32.dll', 'GetFileType');
  if (getFileType !== null) {
    var GetFileType = new NativeFunction(getFileType, 'uint32', ['pointer']);
    log('PIPE_FILETYPE', GetFileType(PIPE_HANDLE));
  }
} catch (e) {
  log('PIPE_FILETYPE_ERROR', e && e.message ? e.message : String(e));
}

hook('kernel32.dll', 'WriteFile', {
  onEnter: function (args) {
    this.handle = args[0];
    if (!isPipeHandle(args[0])) return;
    var len = args[2].toInt32();
    if (bump('WriteFile') <= MAX_EVENTS) log('PIPE_WRITE', preview(args[1], len));
  },
  onLeave: function (retval) {
    if (isPipeHandle(this.handle)) {
      if (bump('WriteFile_leave') <= MAX_EVENTS) log('PIPE_WRITE_RC', retval.toInt32());
    }
  }
});

hook('kernelbase.dll', 'WriteFile', {
  onEnter: function (args) {
    if (!isPipeHandle(args[0])) return;
    var len = args[2].toInt32();
    if (bump('WriteFile_kb') <= MAX_EVENTS) log('PIPE_WRITE_KB', preview(args[1], len));
  }
});

hook('kernel32.dll', 'ReadFile', {
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

hook('kernelbase.dll', 'ReadFile', {
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
    if (bump('ReadFile_kb') <= MAX_EVENTS) log('PIPE_READ_KB', 'rc=' + retval.toInt32(), preview(this.buffer, read));
  }
});

hook('kernel32.dll', 'TransactNamedPipe', {
  onEnter: function (args) {
    if (!isPipeHandle(args[0])) return;
    var inLen = args[2].toInt32();
    var outLen = args[4].toInt32();
    if (bump('TransactNamedPipe') <= MAX_EVENTS) {
      log('PIPE_TRANSACT', 'in=' + preview(args[1], inLen), 'outCap=' + outLen);
    }
  }
});

hook('kernel32.dll', 'CallNamedPipeW', {
  onEnter: function (args) {
    var name = null;
    try { name = args[0].readUtf16String(); } catch (e) {}
    if (name && name.indexOf('EZW_HELPER_PIPE') !== -1) {
      var inLen = args[2].toInt32();
      if (bump('CallNamedPipeW') <= MAX_EVENTS) log('PIPE_CALL', name, 'in=' + preview(args[1], inLen));
    }
  }
});

log('PIPE_PROBE_READY', 'handle=' + PIPE_HANDLE);
