"use strict";
const { Console } = require("console");
const { Writable } = require("stream");
const write = (_, __, callback) => callback();
module.exports = new Console(new Writable({ write, writev: write }));
