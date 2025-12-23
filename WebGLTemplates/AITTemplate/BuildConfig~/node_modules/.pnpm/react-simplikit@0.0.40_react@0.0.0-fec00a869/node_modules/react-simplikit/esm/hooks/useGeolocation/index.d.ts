declare class CustomGeoLocationError extends Error {
    code: number;
    constructor({ code, message }: {
        message: string;
        code: number;
    });
}
type GeolocationData = {
    latitude: number;
    longitude: number;
    accuracy: number;
    altitude: number | null;
    altitudeAccuracy: number | null;
    heading: number | null;
    speed: number | null;
    timestamp: number;
};
declare const GeolocationMountBehavior: {
    readonly GET: "get";
    readonly WATCH: "watch";
};
type GeolocationMountBehaviorType = (typeof GeolocationMountBehavior)[keyof typeof GeolocationMountBehavior];
type GeolocationOptions = {
    mountBehavior?: GeolocationMountBehaviorType;
} & PositionOptions;
/**
 * @description
 * `useGeolocation` is a React hook that retrieves and tracks the user's geographical location.
 * It uses the browser's `Geolocation API` to support both one-time position retrieval and continuous location tracking.
 *
 * @param {GeolocationOptions} [options] - Geolocation options configuration
 * @param {GeolocationMountBehaviorType} [options.mountBehavior] - How the hook behaves on mount:
 *   -- If not provided, no automatic location fetching occurs
 *   -- `get`: automatically fetches location once when component mounts
 *   -- `watch`: automatically starts tracking location changes when component mounts
 * @param {boolean} [options.enableHighAccuracy=false] - If true, provides more accurate position information (increases battery consumption)
 * @param {number} [options.maximumAge=0] - Maximum age in milliseconds of a cached position that is acceptable to return
 * @param {number} [options.timeout=Infinity] - Maximum time (in milliseconds) allowed for the location request
 *
 * @returns {Object} Object containing location data and related functions
 * - loading `boolean` - Whether location data is currently being fetched;
 * - error `CustomGeoLocationError|null` - Error object if an error occurred, or null
 *   The hook uses standard Geolocation API error codes (`1-3`) and adds a custom code (`0`)
 *   : `0` - Geolocation is not supported by the environment
 *   : `1` - User denied permission to access geolocation
 *   : `2` - Position unavailable
 *   : `3` - Timeout - geolocation request took too long;
 * - data `GeolocationData|null` - Location data object or null
 *   : latitude `number` - The latitude in decimal degrees
 *   : longitude `number` - The longitude in decimal degrees
 *   : accuracy `number` - The accuracy of position in meters
 *   : altitude `number|null` - The altitude in meters above the WGS84 ellipsoid
 *   : altitudeAccuracy `number|null` - The altitude accuracy in meters
 *   : heading `number|null` - The heading in degrees clockwise from true north
 *   : speed `number|null` - The speed in meters per second
 *   : timestamp `number` - The time when the position was retrieved;
 * - getCurrentPosition `Function` - Function to get the current position once;
 * - startTracking `Function` - Function to start tracking location changes;
 * - stopTracking `Function` - Function to stop tracking location;
 * - isTracking `boolean` - Whether location tracking is currently active;
 *
 * @example
 * // Basic usage
 * const {
 *   loading,
 *   error,
 *   data,
 *   getCurrentPosition
 * } = useGeolocation();
 *
 * // Automatically fetch location when component mounts
 * const {
 *   loading,
 *   error,
 *   data
 * } = useGeolocation({ mountBehavior: 'get' });
 *
 * // Location tracking
 * const {
 *   loading,
 *   error,
 *   data,
 *   startTracking,
 *   stopTracking,
 *   isTracking
 * } = useGeolocation();
 *
 * const handleStartTracking = () => {
 *   startTracking();
 * };
 *
 * const handleStopTracking = () => {
 *   stopTracking();
 * };
 */
declare function useGeolocation(options?: GeolocationOptions): {
    getCurrentPosition: () => void;
    startTracking: () => void;
    stopTracking: () => void;
    isTracking: boolean;
    loading: boolean;
    error: CustomGeoLocationError | null;
    data: GeolocationData | null;
};

export { useGeolocation };
