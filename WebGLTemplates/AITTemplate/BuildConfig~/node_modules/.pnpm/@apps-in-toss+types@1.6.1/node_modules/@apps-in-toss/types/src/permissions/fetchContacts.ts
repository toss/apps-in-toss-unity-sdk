export interface FetchContactsOptions {
  size: number;
  offset: number;
  query?: {
    contains?: string;
  };
}

/**
 * 연락처 정보를 나타내는 타입이에요.
 */
export interface ContactEntity {
  /** 연락처 이름이에요. */
  name: string;
  /** 연락처 전화번호로, 문자열 형식이에요. */
  phoneNumber: string;
}

export interface ContactResult {
  result: ContactEntity[];
  nextOffset: number | null;
  done: boolean;
}

export type FetchContacts = (options: FetchContactsOptions) => Promise<ContactResult>;
