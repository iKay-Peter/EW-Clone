'use strict';

var MAX_LOG = 400;
var counts = {};
var modulesByName = {};

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
  var addr = findExport(moduleName, exportName);
  if (addr === null) {
    log('HOOK_MISS', moduleName + '!' + exportName);
    return;
  }
  try {
    Interceptor.attach(addr, handlers);
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

function interestingPath(s) {
  if (!s) return false;
  var lower = s.toLowerCase();
  return lower.indexOf('easyworship') !== -1 ||
         lower.indexOf('softouch') !== -1 ||
         lower.indexOf('safemon') !== -1 ||
         lower.indexOf('\\\\.\\pipe\\') !== -1 ||
         lower.indexOf('.db') !== -1 ||
         lower.indexOf('.ew') !== -1 ||
         lower.indexOf('.dat') !== -1 ||
         lower.indexOf('history.ini') !== -1;
}

log('PROBE_START', 'pid=' + Process.id, 'arch=' + Process.arch, 'platform=' + Process.platform);

try {
  var mods = Process.enumerateModules();
  log('MODULE_COUNT', mods.length);
  mods.forEach(function (m) {
    modulesByName[m.name.toLowerCase()] = m;
    log('MODULE', m.name, 'base=' + m.base, 'size=' + m.size, 'path=' + (m.path || ''));
  });
} catch (e) {
  log('MODULE_ERROR', e && e.message ? e.message : String(e));
}

try {
  log('THREAD_COUNT', Process.enumerateThreads().length);
} catch (e) {
  log('THREAD_ERROR', e && e.message ? e.message : String(e));
}

hook('kernel32.dll', 'CreateFileW', {
  onEnter: function (args) {
    var s = readWide(args[0]);
    if (interestingPath(s) && bump('CreateFileW') <= MAX_LOG) {
      log('CreateFileW', s, 'access=' + args[1].toInt32(), 'creation=' + args[4].toInt32());
    }
  }
});

hook('kernelbase.dll', 'CreateFileW', {
  onEnter: function (args) {
    var s = readWide(args[0]);
    if (interestingPath(s) && bump('CreateFileW_kb') <= MAX_LOG) {
      log('CreateFileW_kb', s, 'access=' + args[1].toInt32(), 'creation=' + args[4].toInt32());
    }
  }
});

hook('kernel32.dll', 'CreateNamedPipeW', {
  onEnter: function (args) {
    var s = readWide(args[0]);
    if (bump('CreateNamedPipeW') <= MAX_LOG) {
      log('CreateNamedPipeW', s, 'openMode=' + args[1].toInt32(), 'pipeMode=' + args[2].toInt32(), 'maxInstances=' + args[3].toInt32());
    }
  }
});

hook('kernelbase.dll', 'CreateNamedPipeW', {
  onEnter: function (args) {
    var s = readWide(args[0]);
    if (bump('CreateNamedPipeW_kb') <= MAX_LOG) {
      log('CreateNamedPipeW_kb', s, 'openMode=' + args[1].toInt32(), 'pipeMode=' + args[2].toInt32(), 'maxInstances=' + args[3].toInt32());
    }
  }
});

hook('kernel32.dll', 'CreateProcessW', {
  onEnter: function (args) {
    var app = readWide(args[0]);
    var cmd = readWide(args[1]);
    if (bump('CreateProcessW') <= 50) {
      log('CreateProcessW', 'app=' + app, 'cmd=' + cmd);
    }
  }
});

hook('sqlite3.dll', 'sqlite3_open', {
  onEnter: function (args) {
    var s = readWide(args[0]);
    if (bump('sqlite3_open') <= MAX_LOG) log('sqlite3_open', s);
  }
});

hook('sqlite3.dll', 'sqlite3_open_v2', {
  onEnter: function (args) {
    var s = readWide(args[0]);
    if (bump('sqlite3_open_v2') <= MAX_LOG) log('sqlite3_open_v2', s, 'flags=' + args[1].toInt32(), 'vfs=' + readWide(args[2]));
  }
});

hook('sqlite3.dll', 'sqlite3_create_collation', {
  onEnter: function (args) {
    var s = readWide(args[1]);
    if (bump('sqlite3_create_collation') <= 100) log('sqlite3_create_collation', s, 'textRep=' + args[2].toInt32());
  }
});

hook('sqlite3.dll', 'sqlite3_create_collation_v2', {
  onEnter: function (args) {
    var s = readWide(args[1]);
    if (bump('sqlite3_create_collation_v2') <= 100) log('sqlite3_create_collation_v2', s, 'textRep=' + args[2].toInt32());
  }
});

hook('sqlite3.dll', 'sqlite3_exec', {
  onEnter: function (args) {
    var sql = readWide(args[1]);
    if (sql && bump('sqlite3_exec') <= MAX_LOG) log('sqlite3_exec', sql.substring(0, 500));
  }
});

hook('sqlite3.dll', 'sqlite3_prepare_v2', {
  onEnter: function (args) {
    var sql = readWide(args[1]);
    if (sql && bump('sqlite3_prepare_v2') <= MAX_LOG) log('sqlite3_prepare_v2', sql.substring(0, 500));
  }
});

hook('winhttp.dll', 'WinHttpOpen', {
  onEnter: function (args) {
    if (bump('WinHttpOpen') <= 20) log('WinHttpOpen', readWide(args[0]));
  }
});

hook('winhttp.dll', 'WinHttpConnect', {
  onEnter: function (args) {
    if (bump('WinHttpConnect') <= 20) log('WinHttpConnect', 'host=' + readWide(args[1]), 'port=' + args[2].toInt32());
  }
});

hook('wininet.dll', 'InternetOpenW', {
  onEnter: function (args) {
    if (bump('InternetOpenW') <= 20) log('InternetOpenW', readWide(args[0]));
  }
});

log('PROBE_READY');
