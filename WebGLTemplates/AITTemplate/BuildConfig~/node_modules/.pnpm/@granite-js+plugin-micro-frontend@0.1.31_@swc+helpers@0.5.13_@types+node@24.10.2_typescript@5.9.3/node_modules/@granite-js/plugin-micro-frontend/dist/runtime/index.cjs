
//#region src/runtime/createContainer.ts
function createContainer(name, config) {
	if (typeof global.__MICRO_FRONTEND__.__INSTANCES__[name] === "number") throw new Error(`'${name}' container already registered`);
	const containerIndex = global.__MICRO_FRONTEND__.__INSTANCES__.length;
	const container = {
		name,
		config,
		exposeMap: {}
	};
	Object.defineProperty(global.__MICRO_FRONTEND__.__INSTANCES__, name, {
		value: containerIndex,
		enumerable: false,
		writable: false
	});
	global.__MICRO_FRONTEND__.__INSTANCES__.push(container);
	return container;
}

//#endregion
//#region src/runtime/utils.ts
function getContainer(instanceName) {
	const containerIndex = __MICRO_FRONTEND__.__INSTANCES__[instanceName];
	return typeof containerIndex === "number" ? __MICRO_FRONTEND__.__INSTANCES__[containerIndex] : null;
}
function normalizePath(path) {
	if (path.startsWith("./")) return path.slice(2);
	return path;
}
function parseRemotePath(remotePath) {
	const [remoteName, ...rest] = remotePath.split("/");
	if (remoteName && rest.length > 0) return {
		remoteName,
		modulePath: rest.join("/"),
		fullRequest: remotePath
	};
	throw new Error(`Invalid remote request: ${remotePath}`);
}
function importRemoteModule(remoteRequestPath) {
	const { remoteName, modulePath } = parseRemotePath(remoteRequestPath);
	const container = getContainer(remoteName);
	if (container == null) throw new Error(`${remoteName} container not found`);
	const module$1 = container.exposeMap[normalizePath(modulePath)];
	if (module$1 == null) throw new Error(`Could not resolve '${modulePath}' in ${remoteName} container`);
	return module$1;
}
function toESM(module$1) {
	if (module$1.__esModule) return module$1;
	return Object.defineProperties(module$1, {
		__esModule: { value: true },
		...module$1.default == null ? { default: {
			value: module$1,
			enumerable: true
		} } : null
	});
}

//#endregion
//#region src/runtime/registerShared.ts
function registerShared(libName, module$1) {
	if (global.__MICRO_FRONTEND__.__SHARED__[libName]) throw new Error(`'${libName}' already registered as a shared module`);
	global.__MICRO_FRONTEND__.__SHARED__[libName] = {
		get: () => toESM(module$1),
		loaded: true
	};
}

//#endregion
//#region src/runtime/exposeModule.ts
function exposeModule(container, exposeName, module$1) {
	const normalizedExposeName = normalizePath(exposeName);
	if (container.exposeMap[normalizedExposeName]) throw new Error(`'${exposeName}' is already exposed in ${container.name} container`);
	Object.defineProperty(container.exposeMap, normalizedExposeName, {
		get: () => toESM(module$1),
		enumerable: true
	});
}

//#endregion
exports.createContainer = createContainer;
exports.exposeModule = exposeModule;
exports.getContainer = getContainer;
exports.importRemoteModule = importRemoteModule;
exports.parseRemotePath = parseRemotePath;
exports.registerShared = registerShared;