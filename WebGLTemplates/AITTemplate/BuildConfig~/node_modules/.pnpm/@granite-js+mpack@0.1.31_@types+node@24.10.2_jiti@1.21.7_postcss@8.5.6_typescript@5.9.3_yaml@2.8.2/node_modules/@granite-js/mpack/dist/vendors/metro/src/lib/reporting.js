"use strict";
const chalk = require("chalk");
const stripAnsi = require("strip-ansi");
const util = require("util");
function logWarning(terminal, format, ...args) {
  const str = util.format(format, ...args);
  terminal.log("%s: %s", chalk.yellow("warning"), str);
}
function logError(terminal, format, ...args) {
  terminal.log(
    "%s: %s",
    chalk.red("error"),
    // Syntax errors may have colors applied for displaying code frames
    // in various places outside of where Metro is currently running.
    // If the current terminal does not support color, we'll strip the colors
    // here.
    util.format(chalk.supportsColor ? format : stripAnsi(format), ...args)
  );
}
const nullReporter = { update() {
} };
module.exports = {
  logWarning,
  logError,
  nullReporter
};
