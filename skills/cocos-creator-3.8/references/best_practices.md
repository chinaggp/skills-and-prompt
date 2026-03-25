# Cocos Creator 3.8.x 最佳实践

## 1. 坐标管理与变换 (Transform)
Cocos 3.x 统一了 2D 和 3D 的渲染流水线，坐标使用 `Vec3`。
必须使用 `setPosition` 设置坐标，不能直接修改 `.x` 等字段。

```typescript
// 错误示范
this.node.position.x = 100; // 不生效

// 正确示范
this.node.setPosition(100, this.node.position.y, this.node.position.z);
// 或
const pos = new Vec3(100, 20, 0);
this.node.position = pos;
```

## 2. 资源管理与引用计数 (Asset Bundle & Ref)
放弃老式的 `resources.load` 动态资源调用，全面拥抱 `Asset Bundle` 加载配合手动计数。

- **加载与添加引用**:
```typescript
assetManager.loadBundle('ShopBundle', (err, bundle) => {
    bundle.load('prefab/ShopUI', Prefab, (e, prefab) => {
        prefab.addRef(); // 【重要】手动增加引用
        const node = instantiate(prefab);
        this.node.addChild(node);
    });
});
```
- **释放引用**:
当移除节点/不再使用资源时，必须调 `decRef()` 释放内存。

## 3. 全局事件总线解耦
基于 `EventTarget` 建立全局通知系统，避免组件死互相引用。
```typescript
import { EventTarget } from 'cc';
export const globalEvent = new EventTarget();

// 注册
globalEvent.on('EVENT_NAME', this.onEventTriggered, this);
// 发送
globalEvent.emit('EVENT_NAME', data);
```
