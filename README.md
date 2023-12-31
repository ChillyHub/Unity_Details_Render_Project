# Unity_Details_Render_Project
A solution for rendering terrain details, such as grass, flowers，use GPU instancing



### 效果

https://github.com/ChillyHub/Unity_Details_Render_Project/assets/75598757/37625850-d811-49f5-bef1-0518b40db26a

![屏幕截图 2023-09-01 194531](./README.assets/%E5%B1%8F%E5%B9%95%E6%88%AA%E5%9B%BE%202023-09-01%20194531-1693573977933-6.png)

![屏幕截图 2023-09-01 195712](./README.assets/%E5%B1%8F%E5%B9%95%E6%88%AA%E5%9B%BE%202023-09-01%20195712.png)

![屏幕截图 2023-09-01 202509](./README.assets/%E5%B1%8F%E5%B9%95%E6%88%AA%E5%9B%BE%202023-09-01%20202509-1693574036988-9.png)

![屏幕截图 2023-09-01 202551](./README.assets/%E5%B1%8F%E5%B9%95%E6%88%AA%E5%9B%BE%202023-09-01%20202551.png)

https://github.com/ChillyHub/Unity_Details_Render_Project/assets/75598757/7485fe92-f335-4c74-9313-603ce4c40e18



### 实例数据的管理 

首先，包含位置，缩放，旋转，颜色等信息。数据通过几层传递最终到GPU中。最上层，每个地形块管理一组实例数据，并用四叉树管理数据，用 ScriptableObject 实例化；

第二层，根据相机或角色位置读取周围9个或12个地形块数据（这里的逻辑还没写，暂时全部读取）;

第三层，利用四叉树查找，读取周围一定范围方块内的实例数据，存进一个双缓冲buffer中，区分读写，持续异步更新；

第四层，将读缓冲中的数据读取计算最终数据，如果数量与 ComputeBuffer 中数据不同，则重新分配 ComputeBuffer 并写入数据；

最后，设置 ComputeBuffer 并 Dispatch。 

其次，编辑模式下的更新。笔刷系统暂时决定使用Terrain自带的，通过采样DetailLayer获得数据更新。为了能让笔刷的更新即时可见，需要不停的更新目前更新数据，对整个四叉树重建，所以性能差，需要优化。由于涉及许多只能泡在主线程的计算，目前使用协程拆分任务（由于更新四叉树涉及多层递归嵌套，所以代码挺丑的）。

自带的 DetailPrototype 的数据和自定义的数据结构有些不匹配，而且还不支持多材质和带 LODGroup 组件的 GameObject，故后期肯定要自己写一套detail笔刷工具，工作量大。由于刷实例时要获取地形高度，另外希望实现获取地形某位置贴图颜色的功能，Detials 编辑和 Terrain 编辑不能完全解耦，需要考虑更好的优化方法，减轻四叉树更新负担，更激进的做法是地形系统也自定义，但是一个完整的地形系统工作量太大了。 

另外，中间有频繁的内存 Alloc ，每隔几帧会有较大的GC开销，还要优化（ProjectSetting->Player->OtherSetting->Use incremental GC 能一定层度减少帧数不稳定）。 

![实例数据结构](./README.assets/%E5%AE%9E%E4%BE%8B%E6%95%B0%E6%8D%AE%E7%BB%93%E6%9E%84.png)



### 实例数据的剔除 

ComputeBuffer 使用双缓冲，只加载一定量的实例数据，比如周围200*200米范围的实例，减少处理和传递给GPU的数据量。毕竟存储的只是一些固定的数据，但 Light probes 相关的 SH 和 Occlusion 数据是需要重新计算（如果开启动态GI）后传入GPU的。 

GPU 阶段，就做 frustum 和 HiZ 剔除，这里不做详细赘述。

与此同时，还要计算实例的LOD层级，通过LOD层级和实例类型进行实例重组。使用GPU基数排序算法进行重组，基数范围为0~255，即二进制8位数。末两位表示 LOD，其他位表示实例类型，由此，这套剔除可以支持64种实例类型的同时剔除，每个实例支持4级 LOD，是实现丰富实例渲染的基础。重组的数据存储在 IndirectArgs 中，在渲染实例时会传入 DrawInstancedIndirect 函数。

不过，前面讲过，受限与 DetailPrototype 的不匹配，对 LOD 的设置不完善。由于 GameObject 不支持 LODGroup，目前暂时的办法是将不同 LOD 模型存入不同的 submesh 中，通过 submesh 区分 LOD 层级。不过，这样依然无法解决“利用 LODGroup 组件对每种实例设置不同的 LOD 层级的显示距离”这一目标，目前只是使用统一的 LOD 距离，最终还是得实现一套自定义的 DetailsPrototype 和笔刷工具（一整套Details系统甚至是 Terrain 系统）。 



多实例：

![屏幕截图 2023-09-01 194531](./README.assets/%E5%B1%8F%E5%B9%95%E6%88%AA%E5%9B%BE%202023-09-01%20194531.png)

明显的 LOD 层级：

![屏幕截图 2023-09-01 211018](./README.assets/%E5%B1%8F%E5%B9%95%E6%88%AA%E5%9B%BE%202023-09-01%20211018.png)



PS:（其次，重组时可以对距离划分255个细粒度进行排序，从而实现先画距离近的，减少 overdraw。不过，由于还要兼顾类型的重组，实际运用时要么增加线程数（每组高达上万，GPU线程吃不消），要么进行两次排序（dispatch 数量又多了起来），得不偿失，故没采用） 


PS: 目前有部分实例闪烁的bug，可能是传入GPU的实例mesh vertex index 出问题，也有可能是前面异步代码的错误导致数据传递不如预期？暂时没找到原因。 



### 草地的渲染 

关于草地摆动，就是根据风向和时间，叠加不同频率，相位，振幅的类三角函数得到实际风强度，从而控制草地摆动幅度。 

关于草地渲染，光照上就是直接走PBR渲染。另外，根据草上顶点的相对位置改变albedo，根据视角与水平夹角改变albedo等小调整。还有，为了减少远处草地因法线变化幅度大产生的颜色抖动噪点，选择根据距离，越远的草的法线越倾向于竖直向上，让远处草颜色更统一。 

随后是关于对TAA的支持，为了开启TAA后草地不糊掉，需要写草地的 MotionVector Pass。这里会显得很麻烦，需要计入上一帧的许多东西。如果实例是运动的，位置会改变，自然需要记录上帧的 position 位置，甚至 rotate 信息。然后，如草地这种位置不变，但又顶点动画的，可不记录那么多，当仍需记录上帧时间，以根据风力变形函数重新计算上帧顶点位置。而且，如果风场是动态更新的，还需保存上帧的风场map。目前，并未实现草地的 MotionVector Pass，但是目前看来，应该会增加一定性能消耗。 

![屏幕截图 2023-09-01 210813](./README.assets/%E5%B1%8F%E5%B9%95%E6%88%AA%E5%9B%BE%202023-09-01%20210813.png)



### 草地的交互 

一套交互系统，支持多个交互相机记录物体的 Motion 和 Depth。然后，会使用GPU计算平面2D SDF图。通过交互与被交互物体的 Depth 比较，确定产生交互的范围，再根据这个范围生成sdf图，方便后续根据距离对交互进行更精细的控制（使用 Jump flooding 算法，GPU 算法，复杂度为 O（NlogN），加上多线程，效率不错）。 

然后，可以根据Motion信息，结合固定风场信息和历史帧风场，形成每帧更新的2D混合风场，由此，草会根据交互物体的运动方向摆动，且运动越快，摆动幅度越大。 此外，这套系统还可用于雪地沙地的变化，有了 SDF 图，可以用数学表达式更精细控制变形。风场方面，也可以考虑扩展到3D风场，以实现更多效果。 

https://github.com/ChillyHub/Unity_Details_Render_Project/assets/75598757/b98bab25-8469-4b67-9452-18c47315673e

![屏幕截图 2023-09-01 202509](./README.assets/%E5%B1%8F%E5%B9%95%E6%88%AA%E5%9B%BE%202023-09-01%20202509.png)



### 其他项目中的内容

###### Self

- TAA 抗锯齿
- 卡通与物理结合的大气天空盒子
- 屏幕空间雾效
- 仿原神角色渲染

###### Third Party

- StarterAssets:  第三人称镜头和人物控制
- MagicaCloth2:  角色布料骨骼物理



### Last 

整个上面一套下来，帧数压力已经比较大了，还需要多方面的优化，包括代码层面到模型层面的优化，还要做些加减法。 另外，随着代码复杂度上升，实例数据处理部分的数据结构设计的缺陷也逐渐暴露，不可预期的bug越来越多的出现，可能需要重新设计一套更完善的结构。



![屏幕截图 2023-09-01 202414](./README.assets/%E5%B1%8F%E5%B9%95%E6%88%AA%E5%9B%BE%202023-09-01%20202414.png)

![屏幕截图 2023-09-01 202142](./README.assets/%E5%B1%8F%E5%B9%95%E6%88%AA%E5%9B%BE%202023-09-01%20202142.png)



### 引用

[1] GPU Pro 7: Grass Rendering and Simulation with LOD

[2] GPU Gem 3: [Chapter 16. Vegetation Procedural Animation and Shading in Crysis | NVIDIA Developer](https://developer.nvidia.com/gpugems/gpugems3/part-iii-rendering/chapter-16-vegetation-procedural-animation-and-shading-crysis) 

[3] Jump Flooding Algorithm on Graphics Hardware And Its Applications: [rong-guodong-phd-thesis.pdf (nus.edu.sg)](https://www.comp.nus.edu.sg/~tants/jfa/rong-guodong-phd-thesis.pdf)
