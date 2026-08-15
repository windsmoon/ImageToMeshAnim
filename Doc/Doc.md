# 概述
很多低成本游戏中，立绘都是纯静态图，因为制作 spine 或者 live2d 有较高的时间和金钱成本。即使有了 AI，可以很轻易的出 AI 视频，但是视频播放在游戏中用起来相对麻烦。也可以将视频转成序列帧，但高清立绘做成流畅序列帧，一下加载这么大的内容是很难接受的。
本文提供一个基于 AI 和 mesh 顶点动画的制作类似 live2d 效果的技术，无论是内存占用，还是运行时开销还是制作成本都非常好，目的是给不追求高质量立绘动画的项目一个方案，也能做出类似的效果。下面是仅靠单张立绘（无分层，单张图）做的 idle 效果：

<div align="center">
<video src="MaoNiang_Final.mp4" controls></video>

<video src="Woman.mp4" controls></video>
</div>

## 准备
美术资源只需一张图，没有别的了。制作过程中还需要其他资源，但这些资源都可以用这张图产出，如果本身就有这些资源，那更好。
ffmpeg 和 opencv 库，需要 python 调用，可让 AI agent 帮你安装。
我这里用的 unity 演示，所以最好有 unity cli 和 pipeline 的 package，具体也可以让 agent 帮你装。这一步还挺重要的，因为生成 unity 资源的过程中需要写一些 unity 代码并执行。

## 基本原理
基本原理和 mesh 变形，blend shape 类似，都是通过改变 mesh 的顶点位置来形成动画。而图片生成 mesh，则是利用了 AI 技术（也可以人工制作）。制作完一系列的姿态 mesh 后，把后一个动作的顶点位置和前一个动作的顶点位置相减，得到结果写入相应的 mesh 通道中，再由顶点 shader 根据当前的动画进度把这些通道中存储的值按权重加到原始顶点位置上。

# 具体制作过程
## 生成 mesh
生成 mesh 不是你跟 AI agent 说帮我把这张图片转成 mesh 就行了的，如果直接这么说，它生成的 mesh 很有可能是这样：

<div align="center">
<img src="ErrorMesh.png">
</div>


这个 mesh 的问题在于，有很多非常长的三角形边，看上去是有身体部位的划分，但实际上不能用。这种 mesh，会导致左边的顶点动一下，特别远的顶点也跟着动了，最后全身的动画就会非常奇怪。

我的方法是，先定义一个数据结构，可以让 ai 分析都有哪些部分，在记录每个部位的顶点索引，这样也方便后续生成其他姿态的 mesh 时做参考了。

```
[SerializeField]
private Mesh _mesh;
[SerializeField]
private List<Region> _regionList = new List<Region>();


[Serializable]
public sealed class Region
{
    #region fields
    [SerializeField]
    private string _regionName;
    [SerializeField]
    private int[] _vertexIndices;
    #endregion
    
    #region properties
    public string Name => _regionName;
    public IReadOnlyList<int> VertexIndices => _vertexIndices;
    #endregion
    
    #region methods
    public Region(string name, int[] indices)
    {
        _regionName = name;
        _vertexIndices = indices;
    }
    #endregion
}
```

跟 agent 说，顶点使用规则网格，相邻四点组成小矩形，再拆成两个局部三角形，禁止跨网格、跨透明区域或跨身体部位的长三角形边，这样它生成的 mesh 就是符合要求的了:

<div align="center">
<img src="RightMesh.png">
</div>

关于部位，只看图，ai 可能分析的不一定适用于你要做的动画，最好能直接把你要的动画给他，比如视频，或者描述，这样它分析的准一些。

## 生成姿态 mesh
这一步生成具体的姿态 mesh。我目前使用的方案是最多 13 个关键帧（包括初始姿态）。因为如果一个动作幅度过大，或者有来回，直接从初始姿态变换到最后姿态显然是不行的。

这里我使用的动作是我将一张立绘丢给 AI 生成的一个视频（其实图也是 AI 生成的）：

<div align="center">
<img src="MaoNiang.png">
</div>

<div align="center">
<video src="ActionRefVideo.mp4" controls></video>
</div>


我的工作流，就是让 AI 参考这个这张图和视频，给我生成各个姿态 mesh。

具体的分析过程，大致就是，AI 会用我前面提到的 ffmpeg 和 opencv，来分析视频，把视频里的真正的循环找出来。因为一个视频 10 秒，可能其实是 4 个 2.5 秒不断重复，所以这一步很重要。如果真的按 10 秒来但做一个循环，那么每个关键帧的位置就会出错。

AI 分析好需要好采用哪些关键帧后，就可以给每个关键帧生成对应的 mesh 了。注意，为了保证循环效果，最后一个关键帧要能动画回初始姿态。

## 计算顶点差值
为了方便动画计算，这里我采用了将每个姿态相对于前一个姿态的顶点位置差值求出，并保存到最终的 mesh 通道中。由于我们是 2D 图，所以只需要保存 xy 变化即可，所以 12 个后续姿态，一共有 12 组差值需要保存，即 6 个顶点通道即可，可以选择 Normal，Tangetn，uv1 uv2 等通道，但是 uv0.xy 得留着采样贴图。

```
float3 keyFrame1 : NORMAL;
float4 keyFrame2And3 : TANGENT;
float4 uv0AndKeyFrame4 : TEXCOORD0;
float4 keyFrame5And6 : TEXCOORD1;
float4 keyFrame7And8 : TEXCOORD2;
float4 keyFrame9And10 : TEXCOORD3;
float4 keyFrame11And12 : TEXCOORD4;
```

## shader 计算顶点动画
接下来的事情就简单了，shader 中设置一个动画进度的参数，_AnimationProgress，范围就是 0 ~ 1，使用如下算法进行顶点位置变换即可：

```
float animationProgress = saturate(_AnimationProgress) * (_AnimationCount + 1.0);
float keyFrameProgress = min(animationProgress, _AnimationCount);
positionOS.xy += input.keyFrame1.xy * saturate(keyFrameProgress);
positionOS.xy += input.keyFrame2And3.xy * saturate(keyFrameProgress - 1.0);
positionOS.xy += input.keyFrame2And3.zw * saturate(keyFrameProgress - 2.0);
positionOS.xy += input.uv0AndKeyFrame4.zw * saturate(keyFrameProgress - 3.0);
positionOS.xy += input.keyFrame5And6.xy * saturate(keyFrameProgress - 4.0);
positionOS.xy += input.keyFrame5And6.zw * saturate(keyFrameProgress - 5.0);
positionOS.xy += input.keyFrame7And8.xy * saturate(keyFrameProgress - 6.0);
positionOS.xy += input.keyFrame7And8.zw * saturate(keyFrameProgress - 7.0);
positionOS.xy += input.keyFrame9And10.xy * saturate(keyFrameProgress - 8.0);
positionOS.xy += input.keyFrame9And10.zw * saturate(keyFrameProgress - 9.0);
positionOS.xy += input.keyFrame11And12.xy * saturate(keyFrameProgress - 10.0);
positionOS.xy += input.keyFrame11And12.zw * saturate(keyFrameProgress - 11.0);
float returnProgress = saturate(animationProgress - _AnimationCount);
positionOS.xy = lerp(positionOS.xy, input.positionOS.xy, returnProgress);
```

可以看到，从最后一个姿态回到初始姿态的计算和之前的稍有不同，需要从最后一个姿态插值到初始姿态，而不是直接把变化量加到前面的位置上。

# 注意事项
到目前为止，演示的都是单张图生成，没有任何图片分层。做过 live2d 或者 spine 的同学应该知道，单张图基本上是不够的。比如这里就有一个解决不了的问题，当衣服和皮肤相连的部位有顶点变换时，衣服会带着皮肤一起变换，造成错误的肌肉扭曲变大等效果。这里需要跟 AI 说明这种地方就不要有变换了。

想解决也非常简单，分层即可，前面说的技术，分层后依然可用，让 AI 在生成 mesh 时注意 mesh 相对于原点的坐标即可。

但不幸的是，目前 AI 还无法很好的切图分层，等到 AI 能做这个的时候，我再补后续的工作流。

但我认为本文的内容依然也有意义，现在还有大量游戏尤其是低成本的独立游戏，其立绘，部分场景元素，还依然是静态图，用这种方式可以几乎 0 成本的把这些图转换成动态图，增加一些观感，或者提升一些效率。

以上就是本篇的全部内容了，欢迎讨论。