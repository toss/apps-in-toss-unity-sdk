import { getSchemeUri } from '@granite-js/react-native';

export function getReferrer() {
  try {
    return new URL(getSchemeUri()).searchParams.get('referrer');
  } catch {
    return null;
  }
}
