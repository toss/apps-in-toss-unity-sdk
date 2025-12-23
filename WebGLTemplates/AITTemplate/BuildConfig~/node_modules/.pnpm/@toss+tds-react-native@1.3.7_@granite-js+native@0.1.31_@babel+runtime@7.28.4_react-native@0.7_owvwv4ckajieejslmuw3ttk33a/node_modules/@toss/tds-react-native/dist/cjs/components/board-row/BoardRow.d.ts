import type { ReactNode } from 'react';
export interface BoardRowProps {
    /**
     * `BoardRow` 컴포넌트의 헤더 영역에 표시될 제목이에요.
     */
    title: string;
    /**
     * `BoardRow` 컴포넌트 헤더 영역의 `title` 앞에 표시할 아이콘이에요.
     * 주로 `BoardRow.QIcon` 컴포넌트를 사용해요.
     */
    icon?: ReactNode;
    /**
     * `BoardRow` 컴포넌트의 콘텐츠 영역에 표시될 내용이에요.
     * 주로 `Txt` 혹은 `Post` 컴포넌트로 감싼 요소를 사용해요.
     */
    contents?: ReactNode;
}
declare function BoardRow({ title, icon, contents }: BoardRowProps): import("react/jsx-runtime").JSX.Element;
declare namespace BoardRow {
    var QuestionIcon: () => import("react/jsx-runtime").JSX.Element;
    var QIcon: () => import("react/jsx-runtime").JSX.Element;
}
export default BoardRow;
