"use strict";
const chalk = require("chalk");
const util = require("util");
const groupStack = [];
let collapsedGuardTimer;
module.exports = (terminal, level, mode, ...data) => {
  const logFunction = console[level] && level !== "trace" ? level : "log";
  const color = level === "error" ? chalk.inverse.red : level === "warn" ? chalk.inverse.yellow : chalk.inverse.white;
  if (level === "group") {
    groupStack.push(level);
  } else if (level === "groupCollapsed") {
    groupStack.push(level);
    clearTimeout(collapsedGuardTimer);
    collapsedGuardTimer = setTimeout(() => {
      if (groupStack.includes("groupCollapsed")) {
        terminal.log(
          chalk.inverse.yellow.bold(" WARN "),
          "Expected `console.groupEnd` to be called after `console.groupCollapsed`."
        );
        groupStack.length = 0;
      }
    }, 3e3);
    return;
  } else if (level === "groupEnd") {
    groupStack.pop();
    if (!groupStack.length) {
      clearTimeout(collapsedGuardTimer);
    }
    return;
  }
  if (!groupStack.includes("groupCollapsed")) {
    const lastItem = data[data.length - 1];
    if (typeof lastItem === "string") {
      data[data.length - 1] = lastItem.trimEnd();
    }
    const modePrefix = !mode || mode == "BRIDGE" ? "" : `(${mode.toUpperCase()}) `;
    terminal.log(
      color.bold(` ${modePrefix}${logFunction.toUpperCase()} `) + "".padEnd(groupStack.length * 2, " "),
      // `util.format` actually accepts any arguments.
      // If the first argument is a string, it tries to format it.
      // Otherwise, it just concatenates all arguments.
      // $FlowIssue[incompatible-call] util.format expected the first argument to be a string
      util.format(...data)
    );
  }
};
