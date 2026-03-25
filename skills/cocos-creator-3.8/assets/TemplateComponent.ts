import { _decorator, Component, Node } from 'cc';
const { ccclass, property } = _decorator;

@ccclass('TemplateComponent')
export class TemplateComponent extends Component {
    @property({ type: Node, tooltip: "需要绑定的节点" })
    public targetNode: Node | null = null;

    protected onLoad(): void {
        // onLoad: 节点初始化。不要在这里获取其他节点的组件数据（可能它们还没 onLoad）
    }

    protected start(): void {
        // start: 组件首次激活时触发，可安全获取其他组件状态
    }

    protected update(dt: number): void {
        // 每帧更新逻辑
    }
}
