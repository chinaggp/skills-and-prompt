---
name: cocos-creator-3.8
description: 专门用于处理 Cocos Creator 3.8.x 版本的 TypeScript 游戏开发、组件化架构、资源加载（Asset Manager）和 UI 管理的开发指南。当你需要开发 Cocos 3 游戏组件、处理节点系统、UI 事件系统或者优化性能时可以使用这个技能。包含生命周期、向量变换、Bundle 资源管理与全局事件。
---

# Cocos Creator 3.8 开发技能核心规范

本技能基于 Cocos Creator 3.8.x 现代版本（及基于类 Cocos2d-x 的理念优化）而制定。主要面向 TypeScript 的现代组件化开发流，避免了早期 C++ 繁琐的生命周期且重点涵盖了 3.x 版本独特的内存资源管理与坐标统一变化。

## 快速入口

在开发 Cocos Creator 3.8.x 的代码时，必须遵守以下约定：

1. **类型绑定**：使用 `@ccclass` 与 `@property` 进行明确的面板类型暴露与绑定。
2. **避免组件间在 onLoad 中引用**：跨组件引用请留在 `start()`。
3. **不要改变单独轴向量**：对于节点 Node 变化，必须传入完整对象坐标 `setPosition` 触发底层 setter。
4. **弃用 resources 全局静态加载**：引入按需 bundle 引用机制（`addRef` 与 `decRef` 必须成对出现防止内存泄露）。

## 详尽文档与资源包 (Bundled Resources)

在处理更为复杂的特性时，请查阅以下附加的参考文献和可用模板：

### 参考资料 (References)

- **高级实践指南:** 请查阅 [references/best_practices.md](references/best_practices.md) 获取关于内存管理、强类型监听(`EventTarget`)与 3D 坐标空间统一的完整说明和最佳范例。

### 可复用资产 (Assets)

如果你需要创建任意一个新的业务组件（如 UI 弹窗面板，管理单例）：

- **Cocos 3.8 标准组件模板:** 请使用资产模板 [assets/TemplateComponent.ts](assets/TemplateComponent.ts) 作为基准结构来搭建。
