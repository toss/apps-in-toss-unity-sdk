interface ProgressBarProps {
    value: number;
    total: number;
    activeChar: string;
    inactiveChar: string;
    progressBarSize: number;
    template?: string;
    placeholder?: Placeholder;
}

interface ProgressConfig {
    /**
     * Defaults to `0`.
     */
    value?: number;
    /**
     * Defaults to `100`.
     */
    total?: number;
    /**
     * Defaults to `50` (Depend on terminal size).
     */
    progressBarSize?: ProgressBarProps['progressBarSize'];
    /**
     * Defaults to `'█'`.
     */
    activeChar?: ProgressBarProps['activeChar'];
    /**
     * Defaults to `'░'`.
     */
    inactiveChar?: ProgressBarProps['inactiveChar'];
    /**
     * Template string.
     *
     * Defaults to `'{progress}'`.
     */
    template?: string;
    /**
     * Key-Value data that replace of template's placeholders.
     *
     * Defaults to `{}`.
     */
    placeholder?: Placeholder;
}
type Placeholder = Record<string, string>;
declare const COLOR: unique symbol;
declare const COLOR_ONLY: unique symbol;

interface ProgressBarState extends ProgressBarProps {
    id: number;
    active: boolean;
}
interface ProgressValues {
    /**
     * Current progress value.
     */
    value: number;
    /**
     * Total progress value.
     */
    total?: number;
    /**
     * Key-Value data that replace of template's placeholders.
     */
    placeholder?: Placeholder;
}
declare class ProgressBar {
    private id;
    private active;
    private value;
    private total;
    private placeholder?;
    private config;
    constructor(id: number, config: ProgressConfig);
    start({ value, total, placeholder }: ProgressValues): void;
    stop(): void;
    update({ value, total, placeholder }: ProgressValues): void;
    getState(): ProgressBarState;
}

interface ContainerOptions {
    /**
     *
     */
    refreshRate: number;
}

declare function toColored(text: string, color: string): string;
declare function toColored(color: string): string;

declare const create: (config?: ProgressConfig) => ProgressBar;
declare const remove: (progressBar: ProgressBar) => void;
declare const removeAll: () => void;
declare const count: () => number;
declare const setOptions: (options: ContainerOptions) => void;

export { COLOR, COLOR_ONLY, type Placeholder, ProgressBar, type ProgressBarState, type ProgressConfig, toColored as color, count, create, remove, removeAll, setOptions };
