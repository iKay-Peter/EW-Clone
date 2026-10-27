'use strict';

var MAX_EVENTS = 500;
var counts = {};
var hooked = {};
var dbByHandle = {};
var pipeByHandle = {};

function log() {
  console.log(Array.prototype.slice.call(arguments).join(' '));
}

function bump(key, limit) {
  counts[key] = (counts[key] || 0) + 1;
  if (counts[key] <= (limit || MAX_EVENTS)) {
    return true;
  }
  return false;
}

function findModule(name) {
  try {
    return Process.getModuleByName(name);
  } catch (e) {
    return null;
  }
}

function findExport(moduleName, exportName) {
  try {
    var m = findModule(moduleName);
    if (m && m.getExportByName) return m.getExportByName(exportName);
    if (m && m.findExportByName) return m.findExportByName(exportName);
  } catch (e) {}
  try {
    if (Module.getGlobalExportByName) return Module.getGlobalExportByName(exportName);
  } catch (e) {}
  return null;
}

function readWide(ptr) {
  try {
    return ptr.isNull() ? null : ptr.readUtf16String();
  } catch (e) {
    return null;
  }
}

function readAnsi(ptr) {
  try {
    return ptr.isNull() ? null : ptr.readUtf8String();
  } catch (e) {
    return null;
  }
}

function isInterestingPath(s) {
  if (!s) return false;
  var lower = s.toLowerCase();
  return lower.indexOf('easyworship') !== -1 ||
         lower.indexOf('softouch') !== -1 ||
         lower.indexOf('\\\\.\\pipe\\') !== -1 ||
         lower.indexOf('.db') !== -1 ||
         lower.indexOf('.ew') !== -1 ||
         lower.indexOf('.dat') !== -1 ||
         lower.indexOf('history.ini') !== -1 ||
         lower.indexOf('sqlite3') !== -1 ||
         lower.indexOf('sslsparser') !== -1;
}

function hook(moduleName, exportName, handlers) {
  var key = moduleName.toLowerCase() + '!' + exportName;
  if (hooked[key]) return;
  var addr = findExport(moduleName, exportName);
  if (addr === null) {
    if (bump('HOOK_MISS:' + key, 1)) log('HOOK_MISS', moduleName + '!' + exportName);
    return;
  }
  try {
    Interceptor.attach(addr, handlers);
    hooked[key] = true;
    if (bump('HOOK_OK:' + key, 1)) log('HOOK', moduleName + '!' + exportName, addr);
  } catch (e) {
    log('HOOK_ERROR', moduleName + '!' + exportName, e && e.message ? e.message : String(e));
  }
}

function dbName(dbHandle) {
  try {
    var key = dbHandle.toString();
    return dbByHandle[key] || ('db@' + key);
  } catch (e) {
    return 'db?';
  }
}

function installSqliteHooks() {
  hook('sqlite3.dll', 'sqlite3_open', {
    onEnter: function (args) {
      this.file = readWide(args[0]);
      this.ppDb = args[1];
    },
    onLeave: function (retval) {
      try {
        var db = this.ppDb.readPointer();
        if (!db.isNull()) dbByHandle[db.toString()] = this.file;
      } catch (e) {}
      if (this.file && bump('sqlite3_open', MAX_EVENTS)) log('SQLITE_OPEN', this.file);
    }
  });

  hook('sqlite3.dll', 'sqlite3_open_v2', {
    onEnter: function (args) {
      this.file = readWide(args[0]);
      this.flags = args[1].toInt32();
      this.vfs = readWide(args[2]);
      this.ppDb = args[3];
    },
    onLeave: function (retval) {
      try {
        var db = this.ppDb.readPointer();
        if (!db.isNull()) dbByHandle[db.toString()] = this.file;
      } catch (e) {}
      if (this.file && bump('sqlite3_open_v2', MAX_EVENTS)) {
        log('SQLITE_OPEN_V2', this.file, 'flags=' + this.flags, 'vfs=' + this.vfs, 'rc=' + retval.toInt32());
      }
    }
  });

  hook('sqlite3.dll', 'sqlite3_create_collation', {
    onEnter: function (args) {
      var name = readWide(args[1]);
      if (name && bump('sqlite3_create_collation', 50)) log('SQLITE_COLLATION', name, 'textRep=' + args[2].toInt32());
    }
  });

  hook('sqlite3.dll', 'sqlite3_create_collation_v2', {
    onEnter: function (args) {
      var name = readWide(args[1]);
      if (name && bump('sqlite3_create_collation_v2', 50)) log('SQLITE_COLLATION_V2', name, 'textRep=' + args[2].toInt32());
    }
  });

  hook('sqlite3.dll', 'sqlite3_exec', {
    onEnter: function (args) {
      var sql = readWide(args[1]);
      if (sql && bump('sqlite3_exec', MAX_EVENTS)) {
        log('SQLITE_EXEC', dbName(args[0]), 'sql=' + sql.substring(0, 400).replace(/\s+/g, ' '));
      }
    }
  });

  hook('sqlite3.dll', 'sqlite3_prepare_v2', {
    onEnter: function (args) {
      var sql = readWide(args[1]);
      if (sql && bump('sqlite3_prepare_v2', MAX_EVENTS)) {
        log('SQLITE_PREPARE', dbName(args[0]), 'sql=' + sql.substring(0, 400).replace(/\s+/g, ' '));
      }
    }
  });

  hook('sqlite3.dll', 'sqlite3_prepare16_v2', {
    onEnter: function (args) {
      var sql = readWide(args[1]);
      if (sql && bump('sqlite3_prepare16_v2', MAX_EVENTS)) {
        log('SQLITE_PREPARE16', dbName(args[0]), 'sql=' + sql.substring(0, 400).replace(/\s+/g, ' '));
      }
    }
  });
}

function installFileHooks() {
  hook('kernel32.dll', 'CreateFileW', {
    onEnter: function (args) {
      this.path = readWide(args[0]);
      this.access = args[1].toInt32();
      this.creation = args[4].toInt32();
    },
    onLeave: function (retval) {
      if (!this.path) return;
      var h = retval.toString();
      if (this.path.toLowerCase().indexOf('\\\\.\\pipe\\') === 0) {
        pipeByHandle[h] = this.path;
      }
      if (isInterestingPath(this.path) && bump('CreateFileW', MAX_EVENTS)) {
        log('FILE_OPEN', this.path, 'access=' + this.access, 'creation=' + this.creation, 'handle=' + h);
      }
    }
  });

  hook('kernelbase.dll', 'CreateFileW', {
    onEnter: function (args) {
      this.path = readWide(args[0]);
      this.access = args[1].toInt32();
      this.creation = args[4].toInt32();
    },
    onLeave: function (retval) {
      if (!this.path) return;
      var h = retval.toString();
      if (this.path.toLowerCase().indexOf('\\\\.\\pipe\\') === 0) {
        pipeByHandle[h] = this.path;
      }
      if (isInterestingPath(this.path) && bump('CreateFileW_kb', MAX_EVENTS)) {
        log('FILE_OPEN_KB', this.path, 'access=' + this.access, 'creation=' + this.creation, 'handle=' + h);
      }
    }
  });

  hook('kernel32.dll', 'CreateNamedPipeW', {
    onEnter: function (args) {
      this.name = readWide(args[0]);
      this.openMode = args[1].toInt32();
      this.pipeMode = args[2].toInt32();
      this.maxInstances = args[3].toInt32();
    },
    onLeave: function (retval) {
      if (this.name) {
        pipeByHandle[retval.toString()] = this.name;
        if (bump('CreateNamedPipeW', 100)) {
          log('PIPE_CREATE', this.name, 'openMode=' + this.openMode, 'pipeMode=' + this.pipeMode, 'maxInstances=' + this.maxInstances, 'handle=' + retval);
        }
      }
    }
  });

  hook('kernelbase.dll', 'CreateNamedPipeW', {
    onEnter: function (args) {
      this.name = readWide(args[0]);
    },
    onLeave: function (retval) {
      if (this.name) {
        pipeByHandle[retval.toString()] = this.name;
        if (bump('CreateNamedPipeW_kb', 100)) log('PIPE_CREATE_KB', this.name, 'handle=' + retval);
      }
    }
  });

  hook('kernel32.dll', 'ConnectNamedPipe', {
    onEnter: function (args) {
      var h = args[0].toString();
      var name = pipeByHandle[h] || 'pipe?';
      if (bump('ConnectNamedPipe', 100)) log('PIPE_CONNECT', name, 'handle=' + h);
    }
  });

  hook('kernelbase.dll', 'ConnectNamedPipe', {
    onEnter: function (args) {
      var h = args[0].toString();
      var name = pipeByHandle[h] || 'pipe?';
      if (bump('ConnectNamedPipe_kb', 100)) log('PIPE_CONNECT_KB', name, 'handle=' + h);
    }
  });

  hook('kernel32.dll', 'CreateProcessW', {
    onEnter: function (args) {
      var app = readWide(args[0]);
      var cmd = readWide(args[1]);
      if (bump('CreateProcessW', 50)) log('PROCESS_CREATE', 'app=' + app, 'cmd=' + cmd);
    }
  });

  hook('kernelbase.dll', 'CreateProcessW', {
    onEnter: function (args) {
      var app = readWide(args[0]);
      var cmd = readWide(args[1]);
      if (bump('CreateProcessW_kb', 50)) log('PROCESS_CREATE_KB', 'app=' + app, 'cmd=' + cmd);
    }
  });

  function onLoad(moduleName) {
    if (!moduleName) return;
    var lower = moduleName.toLowerCase();
    if (lower.indexOf('easyworship') !== -1 ||
        lower.indexOf('softouch') !== -1 ||
        lower.indexOf('sqlite3') !== -1 ||
        lower.indexOf('sslsparser') !== -1 ||
        lower.indexOf('ffmpeg') !== -1 ||
        lower.indexOf('ndi') !== -1 ||
        lower.indexOf('webview') !== -1 ||
        lower.indexOf('d3d') !== -1) {
      if (bump('LOAD:' + lower, 1)) log('MODULE_LOAD', moduleName);
    }
    if (lower.indexOf('sqlite3') !== -1) {
      setTimeout(installSqliteHooks, 0);
    }
  }

  hook('kernel32.dll', 'LoadLibraryExW', {
    onEnter: function (args) { this.name = readWide(args[0]); },
    onLeave: function (retval) { if (!retval.isNull()) onLoad(this.name); }
  });

  hook('kernel32.dll', 'LoadLibraryW', {
    onEnter: function (args) { this.name = readWide(args[0]); },
    onLeave: function (retval) { if (!retval.isNull()) onLoad(this.name); }
  });

  hook('kernelbase.dll', 'LoadLibraryExW', {
    onEnter: function (args) { this.name = readWide(args[0]); },
    onLeave: function (retval) { if (!retval.isNull()) onLoad(this.name); }
  });
}

function installNetworkHooks() {
  hook('winhttp.dll', 'WinHttpOpen', {
    onEnter: function (args) {
      if (bump('WinHttpOpen', 20)) log('WINHTTP_OPEN', readWide(args[0]));
    }
  });

  hook('winhttp.dll', 'WinHttpConnect', {
    onEnter: function (args) {
      if (bump('WinHttpConnect', 50)) log('WINHTTP_CONNECT', 'host=' + readWide(args[1]), 'port=' + args[2].toInt32(), 'reserved=' + args[3].toInt32());
    }
  });

  hook('wininet.dll', 'InternetOpenW', {
    onEnter: function (args) {
      if (bump('InternetOpenW', 20)) log('INTERNET_OPEN', readWide(args[0]));
    }
  });

  hook('wininet.dll', 'InternetConnectW', {
    onEnter: function (args) {
      if (bump('InternetConnectW', 50)) log('INTERNET_CONNECT', 'host=' + readWide(args[1]), 'port=' + args[2].toInt32());
    }
  });
}

function installAllHooks() {
  installSqliteHooks();
  installFileHooks();
  installNetworkHooks();
}

log('PROBE_START', 'pid=' + Process.id, 'arch=' + Process.arch, 'platform=' + Process.platform);
try {
  var mods = Process.enumerateModules();
  log('INITIAL_MODULE_COUNT', mods.length);
  mods.forEach(function (m) { log('INITIAL_MODULE', m.name, m.base, m.size, m.path || ''); });
} catch (e) {
  log('MODULE_ERROR', e && e.message ? e.message : String(e));
}

installAllHooks();
setTimeout(installAllHooks, 500);
setTimeout(installAllHooks, 1500);
setTimeout(installAllHooks, 3000);
setTimeout(installAllHooks, 6000);
log('PROBE_READY');
