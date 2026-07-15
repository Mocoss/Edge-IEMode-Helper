let nativePort = null;
let reconnectTimer = null;

// 建立原生消息长连接
function connectNative() {
  if (nativePort) {
    try { nativePort.disconnect(); } catch (e) {}
    nativePort = null;
  }

  nativePort = chrome.runtime.connectNative('com.ietab.helper');

  nativePort.onMessage.addListener((response) => {
    console.log('本地程序响应：', response);
  });

  nativePort.onDisconnect.addListener(() => {
    console.warn('原生连接断开，错误：', chrome.runtime.lastError?.message);
    nativePort = null;
    // 断开后自动重连
    if (!reconnectTimer) {
      reconnectTimer = setTimeout(() => {
        reconnectTimer = null;
        connectNative();
      }, 2000);
    }
  });
}

// 发送命令
function sendCommand(payload) {
  if (!nativePort) connectNative();
  if (nativePort) {
    try {
      nativePort.postMessage(payload);
      return true;
    } catch (e) {
      console.error('发送命令失败：', e);
      return false;
    }
  }
  return false;
}

// 点击扩展图标直接触发：获取当前页URL → 发送IE模式打开命令
chrome.action.onClicked.addListener(async (tab) => {
  if (!tab || !tab.url) return;
  
  sendCommand({
    action: 'openurl',
    url: tab.url
  });
});

// 扩展启动时初始化连接
chrome.runtime.onStartup.addListener(() => connectNative());
connectNative();