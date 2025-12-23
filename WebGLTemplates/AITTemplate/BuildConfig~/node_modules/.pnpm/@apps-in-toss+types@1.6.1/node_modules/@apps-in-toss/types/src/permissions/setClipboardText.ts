export interface SetClipboardTextOptions {
  text: string;
}

export type SetClipboardText = (text: string) => Promise<void>;
