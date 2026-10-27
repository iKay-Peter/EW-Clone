'use strict';

function log() {
  console.log(Array.prototype.slice.call(arguments).join(' '));
}

log('PROBE_START', 'pid=' + Process.id, 'arch=' + Process.arch, 'platform=' + Process.platform);

try {
  var mods = Process.enumerateModules();
  log('MODULE_COUNT', mods.length);
  mods.forEach(function (m) {
    log('MODULE', m.name, m.base, m.size);
  });
} catch (e) {
  log('MODULE_ERROR', e && e.message ? e.message : String(e));
}

try {
  var threads = Process.enumerateThreads();
  log('THREAD_COUNT', threads.length);
  threads.forEach(function (t) {
    log('THREAD', t.id, t.state, t.context ? t.context.pc : '');
  });
} catch (e) {
  log('THREAD_ERROR', e && e.message ? e.message : String(e));
}

function hookExport(moduleName, exportName) {
  var addr = Module.findExportByName(moduleName, exportName);
  if (addr === null) {
    log('HOOK_MISS', moduleName + '!' + exportName);
    return;
  }
  try {
    Interceptor.attach(addr, {
      onEnter: function (args) {
        try {
          var s = args[0].readUtf16String();
          if (s) {
            var lower = s.toLowerCase();
            if (lower.indexOf('pipe') !== -1 ||
                lower.indexOf('easyworship') !== -1 ||
                lower.indexOf('.db') !== -1 ||
                lower.indexOf('.ew') !== -1) {
              log('PATH', exportName, s);
            }
          }
        } catch (e) {}
      }
    });
    log('HOOK', moduleName + '!' + exportName);
  } catch (e) {
    log('HOOK_ERROR', moduleName + '!' + exportName, e && e.message ? e.message : String(e));
  }
}

hookExport('kernel32.dll', 'CreateFileW');
hookExport('kernelbase.dll', 'CreateFileW');
hookExport('kernel32.dll', 'CreateNamedPipeW');
hookExport('kernelbase.dll', 'CreateNamedPipeW');

log('PROBE_READY');
