mergeInto(LibraryManager.library, {
  FlutterYardReady: function () {
    window.yardSceneReady();
  },
  LeaveFlutterYard: function () {
    window.leaveJejuYard();
  }
});
