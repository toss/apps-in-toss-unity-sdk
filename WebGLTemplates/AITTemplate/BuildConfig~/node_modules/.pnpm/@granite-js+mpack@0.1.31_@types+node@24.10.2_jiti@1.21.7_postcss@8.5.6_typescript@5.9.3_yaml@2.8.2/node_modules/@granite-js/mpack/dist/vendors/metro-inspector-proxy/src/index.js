"use strict";
const InspectorProxy = require("./InspectorProxy");
const { parse } = require("url");
function runInspectorProxy(port, projectRoot) {
  const inspectorProxy = new InspectorProxy(projectRoot);
  const app = require("connect")();
  app.use(inspectorProxy.processRequest.bind(inspectorProxy));
  const httpServer = require("http").createServer(app);
  httpServer.listen(port, "127.0.0.1", () => {
    const websocketEndpoints = inspectorProxy.createWebSocketListeners(httpServer);
    httpServer.on("upgrade", (request, socket, head) => {
      const { pathname } = parse(request.url);
      if (pathname != null && websocketEndpoints[pathname]) {
        websocketEndpoints[pathname].handleUpgrade(request, socket, head, (ws) => {
          websocketEndpoints[pathname].emit("connection", ws, request);
        });
      } else {
        socket.destroy();
      }
    });
  });
}
module.exports = { InspectorProxy, runInspectorProxy };
