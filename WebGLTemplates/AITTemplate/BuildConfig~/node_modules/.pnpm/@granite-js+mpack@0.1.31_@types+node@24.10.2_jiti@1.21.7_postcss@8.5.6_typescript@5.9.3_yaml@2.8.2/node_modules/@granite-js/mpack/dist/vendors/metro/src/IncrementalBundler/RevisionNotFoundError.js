"use strict";
class RevisionNotFoundError extends Error {
  revisionId;
  constructor(revisionId) {
    super(`The revision \`${revisionId}\` was not found.`);
    this.revisionId = revisionId;
  }
}
module.exports = RevisionNotFoundError;
