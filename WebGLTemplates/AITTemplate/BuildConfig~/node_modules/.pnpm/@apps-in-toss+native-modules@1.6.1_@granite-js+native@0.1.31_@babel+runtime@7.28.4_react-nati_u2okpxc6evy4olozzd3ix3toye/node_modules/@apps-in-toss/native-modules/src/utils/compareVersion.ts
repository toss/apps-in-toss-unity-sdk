/**
 * Allowed arithmetic operators
 */
export type CompareOperator = '>' | '>=' | '=' | '<' | '<=' | '!=';

/**
 * RegExp to match semver versions like:
 * - 1.0.0
 * - ^1.2.3-alpha+build
 * - v2.3
 */
const SEMVER_REGEX =
  /^[v^~<>=]*?(\d+)(?:\.([x*]|\d+)(?:\.([x*]|\d+)(?:\.([x*]|\d+))?(?:-([\da-z\\-]+(?:\.[\da-z\\-]+)*))?(?:\+[\da-z\\-]+(?:\.[\da-z\\-]+)*)?)?)?$/i;

const isWildcard = (val: string) => ['*', 'x', 'X'].includes(val);

const tryParse = (val: string): string | number => {
  const num = parseInt(val, 10);
  return isNaN(num) ? val : num;
};

const coerceTypes = (a: string | number, b: string | number): [string | number, string | number] => {
  return typeof a === typeof b ? [a, b] : [String(a), String(b)];
};

const compareValues = (a: string, b: string): number => {
  if (isWildcard(a) || isWildcard(b)) {
    return 0;
  }

  const [aVal, bVal] = coerceTypes(tryParse(a), tryParse(b));
  if (aVal > bVal) {
    return 1;
  }
  if (aVal < bVal) {
    return -1;
  }
  return 0;
};

const parseVersion = (version: string): (string | undefined)[] => {
  if (typeof version !== 'string') {
    throw new TypeError('Invalid argument: expected a string');
  }

  const match = version.match(SEMVER_REGEX);
  if (!match) {
    throw new Error(`Invalid semver: '${version}'`);
  }

  const [, major, minor, patch, build, preRelease] = match;
  return [major, minor, patch, build, preRelease];
};

const compareSegments = (a: (string | undefined)[], b: (string | undefined)[]): number => {
  const maxLength = Math.max(a.length, b.length);

  for (let i = 0; i < maxLength; i++) {
    const segA = a[i] ?? '0';
    const segB = b[i] ?? '0';
    const result = compareValues(segA, segB);
    if (result !== 0) {
      return result;
    }
  }

  return 0;
};

/**
 * Compare [semver](https://semver.org/) version strings.
 * Supports versions like `1`, `1.0`, `1.0.0`, and pre-release like `1.0.0-alpha`.
 *
 * @param v1 - First version
 * @param v2 - Second version
 * @returns 1 if v1 > v2, -1 if v1 < v2, 0 if equal
 */
export const compareVersions = (v1: string, v2: string): number => {
  const seg1 = parseVersion(v1);
  const seg2 = parseVersion(v2);

  const preRelease1 = seg1.pop();
  const preRelease2 = seg2.pop();

  const mainCompare = compareSegments(seg1, seg2);
  if (mainCompare !== 0) {
    return mainCompare;
  }

  // If both have pre-release
  if (preRelease1 && preRelease2) {
    return compareSegments(preRelease1.split('.'), preRelease2.split('.'));
  }

  // If only one has pre-release, it's considered lower precedence
  if (preRelease1) {
    return -1;
  }
  if (preRelease2) {
    return 1;
  }

  return 0;
};
