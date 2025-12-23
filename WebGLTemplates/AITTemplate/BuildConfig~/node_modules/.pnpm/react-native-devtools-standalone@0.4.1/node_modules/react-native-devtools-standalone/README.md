<div align="center">

# react-native-devtools-standalone

![preview](./preview.png)

Standalone [react-devtools](https://github.com/facebook/react/tree/main/packages/react-devtools) for integration with React Native

<details>

  <summary>You can also embed react-devtools in <a href="https://reactnative.dev/docs/debugging?js-debugger=new-debugger">New Debugger</a>!</summary>

![preview](./preview-devtools.png)

</details>

</div>

## Architecture

```
┌────────────────────┐         ┌─────────────────────┐
│ Dev Server         │         │ React Native        │
│┌──────────────────┐│         │                     │
││ (Proxy)          ││    ┌────► react-devtools-core │
││ WebSocket Server ◄─────┘    │ (WebSocket)         │
││        ▲         ││         └─────────────────────┘
││        │         ││         ┌─────────────────────────┐
││        ▼         ││         │ React DevTools Frontend │
││ WebSocket Server ◄─────┐    │                         │
│└──────────────────┘│    └────► WebSocket Client        │
└────────────────────┘         └─────────────────────────┘
```

## Installation

```bash
npm install react-native-devtools-standalone
# or
yarn add react-native-devtools-standalone
```

## Usage

### Backend

```ts
import * as http from 'node:http';
import { setupDevToolsProxy } from 'react-native-devtools-standalone/backend';

const server = http.createServer();

const config: DevToolsProxyConfig = /* */;

setupDevToolsProxy(config);

server.listen(...);
```

```ts
interface DevToolsProxyConfig {
  /**
   * Configurations for client(React Native).
   */
  client?: {
    /**
     * Dev server host.
     */
    host?: string;
    /**
     * `__REACT_DEVTOOLS_PORT__` value in React Native runtime.
     *
     * @see https://github.com/facebook/react-native/blob/v0.73.5/packages/react-native/Libraries/Core/setUpReactDevTools.js#L50-L53
     *
     * Defaults to `8097`
     */
    port?: number;
    /**
     * WebSocket delegate
     */
    delegate?: ProxyWebSocketDelegate;
  };
  /**
   * Configurations for React DevTools.
   */
  devtools?: {
    /**
     * Dev server host.
     */
    host?: string;
    /**
     * Port for DevTools to connect.
     *
     * Defaults to `8098`
     */
    port?: number;
    /**
     * WebSocket delegate
     */
    delegate?: ProxyWebSocketDelegate;
  };
}
```

### Frontend

```ts
import { setupDevTools } from 'react-native-devtools-standalone/frontend';

const config: DevToolsConfigs = {
  element: document.getElementById('container'),
  /* */
};

setupDevTools(config);
```

```ts
import type {
  Config as DevtoolsStoreConfig,
  DevtoolsProps,
} from 'react-devtools-inline/frontend';

interface DevToolsConfigs {
  /**
   * Element to render DevTools.
   */
  element: HTMLElement;
  /**
   * Proxy web socket server host.
   *
   * Defaults to `'localhost'`
   */
  host?: string;
  /**
   * Proxy web socket server port.
   *
   * Defaults to `8098`
   */
  port?: number;
  /**
   * React DevTools store config.
   */
  devtoolsStoreConfig?: DevtoolsStoreConfig;
  /**
   * React DevTools props.
   *
   * Defaults to `{ showTabBar: true, hideViewSourceAction: true }`
   */
  devtoolsProps?: DevtoolsProps;
  /**
   * WebSocket delegate.
   */
  delegate?: ProxyWebSocketDelegate;
}
```

## Development

```bash
# start dev server (http://localhost:3000/index.html)
yarn start
```

## License

[MIT](./LICENSE)
