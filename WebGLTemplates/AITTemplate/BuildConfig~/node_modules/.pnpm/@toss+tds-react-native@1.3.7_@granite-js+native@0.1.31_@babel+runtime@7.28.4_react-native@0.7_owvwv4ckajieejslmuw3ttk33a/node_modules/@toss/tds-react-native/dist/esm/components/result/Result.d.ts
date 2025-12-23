import type { ReactNode } from 'react';
import type { StyleProp, ViewStyle } from 'react-native';
import { ResultButton } from './ResultButton';
export interface ResultProps {
    /**
     * `Result` 컴포넌트의 `title` 위에 표시할 시각적 요소로, 주로 아이콘이나 이미지를 나타내요. `Asset` 컴포넌트를 사용해서 다양한 시각적 콘텐츠를 표현할 수 있어요.
     */
    figure?: ReactNode;
    /**
     * `Result` 컴포넌트의 결과 화면의 제목이에요. 사용자가 어떤 작업을 한 뒤의 성공 여부 같은 상태를 간결하게 전달하는 데에 사용해요.
     */
    title?: ReactNode;
    /**
     * `Result` 컴포넌트의 `title` 아래에 추가로 설명을 제공하는 영역이에요. 좀 더 자세한 정보를 제공할 때 사용해요.
     */
    description?: ReactNode;
    /**
     * `Result` 컴포넌트의 `description` 아래에 표시할 버튼이에요. `Result.Button` 컴포넌트를 사용해서 다시 시도하기, 홈으로 돌아가기 등과 같은 액션을 추가할 수 있어요.
     */
    button?: ReactNode;
    /**
     * `Result` 컴포넌트의 스타일을 변경할 때 사용해요.
     */
    style?: StyleProp<ViewStyle>;
}
/**
 *
 * @example
 * <Result
 *   figure={<Icon name="icon-info-circle" />}
 *   title={`다시 접속해주세요`}
 *   description={`페이지를 불러올 수 없습니다 \n다시 시도해주세요`}
 *   button={<Button size="medium">재시도</Button>}
 * />
 */
declare function Result({ figure, title, description, button, style }: ResultProps): import("react/jsx-runtime").JSX.Element;
declare namespace Result {
    var Button: typeof ResultButton;
}
export default Result;
