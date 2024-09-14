# Vision_Collection
收集一些视觉效果有趣的项目，理解它们的实现，并进行优化。

## 展示效果
### Superliminal + ViewFinder
![Superliminal + ViewFinder](./src/viewfinderAndSuperliminal.gif)

### Moncage
这是更改过的Moncage，原项目是使用StencilBuffer实现的，这会导致一些耦合。我使用一个简单的shader，将立方体的六个面分别映射到一个平面上，然后在平面上进行渲染。这样的实现方式更加灵活，可以适用于任何场景，不需要额外的操作。
Hierachy结构如下：
![Moncage](./src/moncage_structure.png)



## 参考来源
### ViewFinder
参考：https://github.com/NotRiemannCousin/ViewFinder-Copy
### Moncage
参考：https://github.com/evakimox/WorldCube
