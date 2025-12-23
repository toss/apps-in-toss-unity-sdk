"use strict";
const throttle = require("lodash.throttle");
const readline = require("readline");
const tty = require("tty");
const util = require("util");
function clearStringBackwards(stream, str) {
  readline.moveCursor(stream, -stream.columns, 0);
  readline.clearLine(stream, 0);
  let lineCount = (str.match(/\n/g) || []).length;
  while (lineCount > 0) {
    readline.moveCursor(stream, 0, -1);
    readline.clearLine(stream, 0);
    --lineCount;
  }
}
function chunkString(str, size) {
  const ANSI_COLOR = "\x1B\\[([0-9]{1,2}(;[0-9]{1,2})?)?m";
  const SKIP_ANSI = `(?:${ANSI_COLOR})*`;
  return str.match(new RegExp(`(?:${SKIP_ANSI}.){1,${size}}`, "g")) || [];
}
function getTTYStream(stream) {
  if (stream instanceof tty.WriteStream && stream.isTTY && stream.columns >= 1) {
    return stream;
  }
  return null;
}
class Terminal {
  _logLines;
  _nextStatusStr;
  _scheduleUpdate;
  _statusStr;
  _stream;
  constructor(stream) {
    this._logLines = [];
    this._nextStatusStr = "";
    this._scheduleUpdate = throttle(this._update, 33);
    this._statusStr = "";
    this._stream = stream;
  }
  /**
   * Clear and write the new status, logging in bulk in-between. Doing this in a
   * throttled way (in a different tick than the calls to `log()` and
   * `status()`) prevents us from repeatedly rewriting the status in case
   * `terminal.log()` is called several times.
   */
  _update() {
    const { _statusStr, _stream } = this;
    const ttyStream = getTTYStream(_stream);
    if (_statusStr === this._nextStatusStr && this._logLines.length === 0) {
      return;
    }
    if (ttyStream != null) {
      clearStringBackwards(ttyStream, _statusStr);
    }
    this._logLines.forEach((line) => {
      _stream.write(line);
      _stream.write("\n");
    });
    this._logLines = [];
    if (ttyStream != null) {
      this._nextStatusStr = chunkString(this._nextStatusStr, ttyStream.columns).join("\n");
      _stream.write(this._nextStatusStr);
    }
    this._statusStr = this._nextStatusStr;
  }
  /**
   * Shows some text that is meant to be overriden later. Return the previous
   * status that was shown and is no more. Calling `status()` with no argument
   * removes the status altogether. The status is never shown in a
   * non-interactive terminal: for example, if the output is redirected to a
   * file, then we don't care too much about having a progress bar.
   */
  status(format, ...args) {
    const { _nextStatusStr } = this;
    this._nextStatusStr = util.format(format, ...args);
    this._scheduleUpdate();
    return _nextStatusStr;
  }
  /**
   * Similar to `console.log`, except it moves the status/progress text out of
   * the way correctly. In non-interactive terminals this is the same as
   * `console.log`.
   */
  log(format, ...args) {
    this._logLines.push(util.format(format, ...args));
    this._scheduleUpdate();
  }
  /**
   * Log the current status and start from scratch. This is useful if the last
   * status was the last one of a series of updates.
   */
  persistStatus() {
    this.log(this._nextStatusStr);
    this._nextStatusStr = "";
  }
  flush() {
    this._scheduleUpdate.flush();
  }
}
module.exports = Terminal;
