/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function escapeHTML(text) {
  return text.replace(/[&<>'"]/g, function (entity) {
    switch (entity) {
      case '&':
        return '&amp;';

      case '<':
        return '&lt;';

      case '>':
        return '&gt;';

      case "'":
        return '&#39;';

      case '"':
        return '&quot;';

      default:
        return entity;
    }
  });
}

export { escapeHTML };
