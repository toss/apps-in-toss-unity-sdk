import type { StandardSchemaV1 } from '@standard-schema/spec';
/**
 * Validates route parameters using either a StandardSchema or a validation function.
 *
 * @param validateParams - The validation schema or function to apply
 * @param parsedParams - The parsed parameters to validate
 * @returns The validated parameters
 * @throws Error if validation fails or async validation is attempted
 */
export declare function validateRouteParams(validateParams: ((params: Readonly<object | undefined>) => any) | StandardSchemaV1<any, any>, parsedParams: Readonly<object | undefined>): any;
