"use strict";
var import_graphOperations = require("./graphOperations");
const { EventEmitter } = require("events");
class DeltaCalculator extends EventEmitter {
  _changeEventSource;
  _options;
  _currentBuildPromise;
  _deletedFiles = /* @__PURE__ */ new Set();
  _modifiedFiles = /* @__PURE__ */ new Set();
  _addedFiles = /* @__PURE__ */ new Set();
  _graph;
  constructor(entryPoints, changeEventSource, options) {
    super();
    this._options = options;
    this._changeEventSource = changeEventSource;
    this._graph = (0, import_graphOperations.createGraph)({
      entryPoints,
      transformOptions: this._options.transformOptions
    });
    this._changeEventSource.on("change", this._handleMultipleFileChanges);
  }
  /**
   * Stops listening for file changes and clears all the caches.
   */
  end() {
    this._changeEventSource.removeListener("change", this._handleMultipleFileChanges);
    this.removeAllListeners();
    this._graph = (0, import_graphOperations.createGraph)({
      entryPoints: this._graph.entryPoints,
      transformOptions: this._options.transformOptions
    });
    this._modifiedFiles = /* @__PURE__ */ new Set();
    this._deletedFiles = /* @__PURE__ */ new Set();
    this._addedFiles = /* @__PURE__ */ new Set();
  }
  /**
   * Main method to calculate the delta of modules. It returns a DeltaResult,
   * which contain the modified/added modules and the removed modules.
   */
  async getDelta({ reset, shallow }) {
    if (this._currentBuildPromise) {
      await this._currentBuildPromise;
    }
    const modifiedFiles = this._modifiedFiles;
    this._modifiedFiles = /* @__PURE__ */ new Set();
    const deletedFiles = this._deletedFiles;
    this._deletedFiles = /* @__PURE__ */ new Set();
    const addedFiles = this._addedFiles;
    this._addedFiles = /* @__PURE__ */ new Set();
    this._currentBuildPromise = this._getChangedDependencies(modifiedFiles, deletedFiles, addedFiles);
    let result;
    const numDependencies = this._graph.dependencies.size;
    try {
      result = await this._currentBuildPromise;
    } catch (error) {
      modifiedFiles.forEach((file) => this._modifiedFiles.add(file));
      deletedFiles.forEach((file) => this._deletedFiles.add(file));
      addedFiles.forEach((file) => this._addedFiles.add(file));
      if (this._graph.dependencies.size !== numDependencies) {
        this._graph.dependencies = /* @__PURE__ */ new Map();
      }
      throw error;
    } finally {
      this._currentBuildPromise = null;
    }
    if (reset) {
      (0, import_graphOperations.reorderGraph)(this._graph, { shallow });
      return {
        added: this._graph.dependencies,
        modified: /* @__PURE__ */ new Map(),
        deleted: /* @__PURE__ */ new Set(),
        reset: true
      };
    }
    return result;
  }
  /**
   * Returns the graph with all the dependencies. Each module contains the
   * needed information to do the traversing (dependencies, inverseDependencies)
   * plus some metadata.
   */
  getGraph() {
    return this._graph;
  }
  /* $FlowFixMe[missing-local-annot] The type annotation(s) required by Flow's
   * LTI update could not be added via codemod */
  _handleMultipleFileChanges = ({ eventsQueue }) => {
    eventsQueue.forEach(this._handleFileChange);
  };
  /**
   * Handles a single file change. To avoid doing any work before it's needed,
   * the listener only stores the modified file, which will then be used later
   * when the delta needs to be calculated.
   */
  _handleFileChange = ({ type, filePath }) => {
    let state;
    if (this._deletedFiles.has(filePath)) {
      state = "deleted";
    } else if (this._modifiedFiles.has(filePath)) {
      state = "modified";
    } else if (this._addedFiles.has(filePath)) {
      state = "added";
    }
    let nextState;
    if (type === "delete") {
      nextState = "deleted";
    } else if (type === "add") {
      nextState = state === "deleted" ? "modified" : "added";
    } else {
      nextState = state === "added" ? "added" : "modified";
    }
    switch (nextState) {
      case "deleted":
        this._deletedFiles.add(filePath);
        this._modifiedFiles.delete(filePath);
        this._addedFiles.delete(filePath);
        break;
      case "added":
        this._addedFiles.add(filePath);
        this._deletedFiles.delete(filePath);
        this._modifiedFiles.delete(filePath);
        break;
      case "modified":
        this._modifiedFiles.add(filePath);
        this._deletedFiles.delete(filePath);
        this._addedFiles.delete(filePath);
        break;
      default:
        nextState;
    }
    this.emit("change");
  };
  async _getChangedDependencies(modifiedFiles, deletedFiles, addedFiles) {
    if (!this._graph.dependencies.size) {
      const { added: added2 } = await (0, import_graphOperations.initialTraverseDependencies)(this._graph, this._options);
      return {
        added: added2,
        modified: /* @__PURE__ */ new Map(),
        deleted: /* @__PURE__ */ new Set(),
        reset: true
      };
    }
    deletedFiles.forEach((filePath) => {
      const module2 = this._graph.dependencies.get(filePath);
      if (module2) {
        module2.inverseDependencies.forEach((path) => {
          if (!deletedFiles.has(path)) {
            modifiedFiles.add(path);
          }
        });
      }
    });
    if (this._options.unstable_allowRequireContext) {
      addedFiles.forEach((filePath) => {
        (0, import_graphOperations.markModifiedContextModules)(this._graph, filePath, modifiedFiles);
      });
    }
    const modifiedDependencies = Array.from(modifiedFiles).filter((filePath) => this._graph.dependencies.has(filePath));
    if (modifiedDependencies.length === 0) {
      return {
        added: /* @__PURE__ */ new Map(),
        modified: /* @__PURE__ */ new Map(),
        deleted: /* @__PURE__ */ new Set(),
        reset: false
      };
    }
    const { added, modified, deleted } = await (0, import_graphOperations.traverseDependencies)(modifiedDependencies, this._graph, this._options);
    return {
      added,
      modified,
      deleted,
      reset: false
    };
  }
}
module.exports = DeltaCalculator;
