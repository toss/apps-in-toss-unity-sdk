export declare function clamp(value: number, min?: number, max?: number): number;
/**
 * HTML input range의 값을 모사한 함수
 * min, max값의 사이에 있다면 min/max값을 반환하고, 그외의 경우 min에서 가장 가까운 step만큼 움직인 값을 반환한다
 */
export declare function getNearestSteppedValue(value: number, min: number, max: number, step: number): number;
