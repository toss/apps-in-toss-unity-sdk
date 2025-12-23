import { Frame } from './blocks/Frame';
import { ContentIcon } from './blocks/ContentIcon';
import { ContentImage } from './blocks/ContentImage';
import { ContentLottie } from './blocks/ContentLottie';
import { Icon } from './Icon';
import { Image } from './Image';
import { Lottie } from './Lottie';
import { Text } from './Text';
import { frameShape } from './presets/frameShape';
interface CompoundComposition {
    Frame: typeof Frame;
    ContentIcon: typeof ContentIcon;
    ContentImage: typeof ContentImage;
    ContentLottie: typeof ContentLottie;
    Icon: typeof Icon;
    Image: typeof Image;
    Lottie: typeof Lottie;
    Text: typeof Text;
    frameShape: typeof frameShape;
}
export declare const Asset: (({ resource, frame, union, style }: import("./Asset").AssetProps) => import("react/jsx-runtime").JSX.Element) & CompoundComposition;
export { frameShape } from './presets/frameShape';
