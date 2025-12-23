import { describe, it, expect } from 'vitest';
import { compareVersions } from './compareVersion.js';

describe('compareVersions', () => {
  it('기본 버전 비교가 올바르게 작동해야 합니다', () => {
    expect(compareVersions('1.0.0', '1.0.0')).toBe(0);
    expect(compareVersions('1.0.0', '1.0.1')).toBe(-1);
    expect(compareVersions('1.0.1', '1.0.0')).toBe(1);
    expect(compareVersions('1.1.0', '1.0.0')).toBe(1);
    expect(compareVersions('2.0.0', '1.0.0')).toBe(1);
  });

  it('다양한 길이의 버전을 비교할 수 있어야 합니다', () => {
    expect(compareVersions('1', '1.0.0')).toBe(0);
    expect(compareVersions('1.0', '1.0.0')).toBe(0);
    expect(compareVersions('1', '2')).toBe(-1);
    expect(compareVersions('1.0', '1.1')).toBe(-1);
    expect(compareVersions('1.1', '1.0.1')).toBe(1);
    expect(compareVersions('11.1.10', '11.0')).toBe(1);
    expect(compareVersions('1.1.1', '1')).toBe(1);
    expect(compareVersions('01.1.0', '1.01')).toBe(0);
    expect(compareVersions('10.0.0', '10.114')).toBe(-1);
    expect(compareVersions('1.0', '1.4.1')).toBe(-1);
  });

  it('와일드카드 버전을 올바르게 처리해야 합니다', () => {
    expect(compareVersions('1.x', '1.0.0')).toBe(0);
    expect(compareVersions('1.*', '1.1.0')).toBe(0);
    expect(compareVersions('1.0.X', '1.0.1')).toBe(0);
    expect(compareVersions('3', '3.x.x')).toBe(0);
    expect(compareVersions('3.3', '3.x.x')).toBe(0);
    expect(compareVersions('3.3.3', '3.x.x')).toBe(0);
    expect(compareVersions('3.x.x', '3.3.3')).toBe(0);
    expect(compareVersions('3.3.3', '3.X.X')).toBe(0);
    expect(compareVersions('3.3.3', '3.3.x')).toBe(0);
    expect(compareVersions('3.3.3', '3.*.*')).toBe(0);
    expect(compareVersions('3.3.3', '3.3.*')).toBe(0);
    expect(compareVersions('3.0.3', '3.0.*')).toBe(0);
    expect(compareVersions('0.7.x', '0.6.0')).toBe(1);
    expect(compareVersions('0.7.x', '0.6.0-asdf')).toBe(1);
    expect(compareVersions('0.7.x', '0.6.2')).toBe(1);
    expect(compareVersions('0.7.x', '0.7.0-asdf')).toBe(1);
    expect(compareVersions('1.2.*', '1.1.3')).toBe(1);
    expect(compareVersions('1.2.*', '1.1.9999')).toBe(1);
    expect(compareVersions('1.2.x', '1.0.0')).toBe(1);
    expect(compareVersions('1.2.x', '1.1.0')).toBe(1);
    expect(compareVersions('1.2.x', '1.1.3')).toBe(1);
    expect(compareVersions('2.*.*', '1.0.1')).toBe(1);
    expect(compareVersions('2.*.*', '1.1.3')).toBe(1);
    expect(compareVersions('2.x.x', '1.0.0')).toBe(1);
    expect(compareVersions('2.x.x', '1.1.3')).toBe(1);
  });

  it('프리릴리스 버전을 올바르게 비교해야 합니다', () => {
    expect(compareVersions('1.0.0-alpha', '1.0.0')).toBe(-1);
    expect(compareVersions('1.0.0', '1.0.0-alpha')).toBe(1);
    expect(compareVersions('1.0.0-alpha', '1.0.0-beta')).toBe(-1);
    expect(compareVersions('1.0.0-beta.2', '1.0.0-beta.1')).toBe(1);
    expect(compareVersions('1.0.0-alpha.1', '1.0.0-alpha')).toBe(1);
    expect(compareVersions('1.0.0-alpha', '1.0.0-alpha.1')).toBe(-1);
    expect(compareVersions('1.0.0-alpha.1', '1.0.0-alpha.beta')).toBe(-1);
    expect(compareVersions('1.0.0-alpha.beta', '1.0.0-beta')).toBe(-1);
    expect(compareVersions('1.0.0-beta', '1.0.0-beta.2')).toBe(-1);
    expect(compareVersions('1.0.0-beta.2', '1.0.0-beta.11')).toBe(-1);
    expect(compareVersions('1.0.0-beta.11', '1.0.0-rc.1')).toBe(-1);
    expect(compareVersions('1.0.0-rc.1', '1.0.0')).toBe(-1);
    expect(compareVersions('1.0.0-alpha', '1')).toBe(-1);
    expect(compareVersions('1.0.0-beta.11', '1.0.0-beta.1')).toBe(1);
    expect(compareVersions('1.0.0-beta.10', '1.0.0-beta.9')).toBe(1);
    expect(compareVersions('1.0.0-beta.10', '1.0.0-beta.90')).toBe(-1);
    expect(compareVersions('0.6.1-1', '0.6.1-0')).toBe(1);
    expect(compareVersions('v0.5.4-pre', '0.5.4-alpha')).toBe(1);
  });

  it('접두사가 있는 버전을 올바르게 처리해야 합니다', () => {
    expect(compareVersions('v1.0.0', '1.0.0')).toBe(0);
    expect(compareVersions('^1.0.0', '1.0.0')).toBe(0);
    expect(compareVersions('~1.0.0', '1.0.0')).toBe(0);
    expect(compareVersions('v1.0.0', 'v1.0.0')).toBe(0);
    expect(compareVersions('v1.0.0-alpha', '1.0.0-alpha')).toBe(0);
    expect(compareVersions('v3.2.1', 'v2.3.2')).toBe(1);
  });

  it('선행 0을 무시해야 합니다', () => {
    expect(compareVersions('01.0.0', '1')).toBe(0);
    expect(compareVersions('01.0.0', '1.0.0')).toBe(0);
    expect(compareVersions('1.01.0', '1.01.0')).toBe(0);
    expect(compareVersions('1.0.03', '1.0.3')).toBe(0);
    expect(compareVersions('1.0.03-alpha', '1.0.3-alpha')).toBe(0);
    expect(compareVersions('v01.0.0', '1.0.0')).toBe(0);
    expect(compareVersions('v01.0.0', '2.0.0')).toBe(-1);
  });

  it('빌드 메타데이터를 무시해야 합니다', () => {
    expect(compareVersions('1.4.0-build.3928', '1.4.0-build.3928+sha.a8d9d4f')).toBe(0);
    expect(compareVersions('1.4.0-build.3928+sha.b8dbdb0', '1.4.0-build.3928+sha.a8d9d4f')).toBe(0);
    expect(compareVersions('1.0.0-alpha+001', '1.0.0-alpha')).toBe(0);
    expect(compareVersions('1.0.0-beta+exp.sha.5114f85', '1.0.0-beta+exp.sha.999999')).toBe(0);
    expect(compareVersions('1.0.0+20130313144700', '1.0.0')).toBe(0);
    expect(compareVersions('1.0.0+20130313144700', '2.0.0')).toBe(-1);
    expect(compareVersions('1.0.0+20130313144700', '1.0.1+11234343435')).toBe(-1);
    expect(compareVersions('1.0.1+1', '1.0.1+2')).toBe(0);
    expect(compareVersions('1.0.0+a-a', '1.0.0+a-b')).toBe(0);
  });

  it('4세그먼트 버전을 올바르게 처리해야 합니다', () => {
    expect(compareVersions('1.0.0.0', '1')).toBe(0);
    expect(compareVersions('1.0.0.0', '1.0')).toBe(0);
    expect(compareVersions('1.0.0.0', '1.0.0')).toBe(0);
    expect(compareVersions('1.0.0.0', '1.0.0.0')).toBe(0);
    expect(compareVersions('1.2.3.4', '1.2.3.4')).toBe(0);
    expect(compareVersions('1.2.3.4', '1.2.3.04')).toBe(0);
    expect(compareVersions('v1.2.3.4', '01.2.3.4')).toBe(0);
    expect(compareVersions('1.2.3.4', '1.2.3.5')).toBe(-1);
    expect(compareVersions('1.2.3.5', '1.2.3.4')).toBe(1);
    expect(compareVersions('1.0.0.0-alpha', '1.0.0-alpha')).toBe(0);
    expect(compareVersions('1.0.0.0-alpha', '1.0.0.0-beta')).toBe(-1);
  });

  it('단일 세그먼트 버전을 올바르게 비교해야 합니다', () => {
    expect(compareVersions('10', '9')).toBe(1);
    expect(compareVersions('10', '10')).toBe(0);
    expect(compareVersions('9', '10')).toBe(-1);
  });

  it('두 세그먼트 버전을 올바르게 비교해야 합니다', () => {
    expect(compareVersions('10.8', '10.4')).toBe(1);
    expect(compareVersions('10.1', '10.1')).toBe(0);
    expect(compareVersions('10.1', '10.2')).toBe(-1);
  });

  it('세 세그먼트 버전을 올바르게 비교해야 합니다', () => {
    expect(compareVersions('10.1.8', '10.0.4')).toBe(1);
    expect(compareVersions('10.0.1', '10.0.1')).toBe(0);
    expect(compareVersions('10.1.1', '10.2.2')).toBe(-1);
    expect(compareVersions('11.0.10', '11.0.2')).toBe(1);
    expect(compareVersions('11.0.2', '11.0.10')).toBe(-1);
  });

  it('추가 비교 테스트', () => {
    expect(compareVersions('0.1.21', '0.1.5')).toBe(1);
    expect(compareVersions('1', '0.0.0-beta')).toBe(1);
    expect(compareVersions('1', '0.2.3')).toBe(1);
    expect(compareVersions('1', '0.2.4')).toBe(1);
    expect(compareVersions('1', '1.0.0-0')).toBe(1);
    expect(compareVersions('1', '1.0.0-beta')).toBe(1);
    expect(compareVersions('1.0', '0.0.0')).toBe(1);
    expect(compareVersions('1.0', '0.1.0')).toBe(1);
    expect(compareVersions('1.0', '0.1.2')).toBe(1);
    expect(compareVersions('1.0.0', '0.0.0')).toBe(1);
    expect(compareVersions('1.0.0', '0.0.1')).toBe(1);
    expect(compareVersions('1.0.0', '0.2.3')).toBe(1);
    expect(compareVersions('1.2.2', '1.2.1')).toBe(1);
    expect(compareVersions('2', '1.0.0')).toBe(1);
    expect(compareVersions('2', '1.0.0-beta')).toBe(1);
    expect(compareVersions('2', '1.9999.9999')).toBe(1);
    expect(compareVersions('2.0.0', '1.0.0')).toBe(1);
    expect(compareVersions('2.0.0', '1.1.1')).toBe(1);
    expect(compareVersions('2.0.0', '1.2.9')).toBe(1);
    expect(compareVersions('2.0.0', '1.9999.9999')).toBe(1);
    expect(compareVersions('2.3', '2.2.1')).toBe(1);
    expect(compareVersions('2.3', '2.2.2')).toBe(1);
    expect(compareVersions('2.4', '2.3.0')).toBe(1);
    expect(compareVersions('2.4', '2.3.5')).toBe(1);
    expect(compareVersions('3.2.1', '2.3.2')).toBe(1);
    expect(compareVersions('3.2.1', '3.2.0')).toBe(1);
  });

  it('잘못된 버전 형식에 대해 오류를 발생시켜야 합니다', () => {
    expect(() => compareVersions('invalid', '1.0.0')).toThrow();
    expect(() => compareVersions('1.0.0', 'invalid')).toThrow();
    expect(() => compareVersions('6.3.', '1.0.0')).toThrow();
    expect(() => compareVersions('1.2.3a', '1.0.0')).toThrow();
    expect(() => compareVersions('1.2.-3a', '1.0.0')).toThrow();
  });
});
