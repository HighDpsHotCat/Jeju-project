"""Export torchvision LRASPP MobileNetV3 COCO/VOC weights for the Flutter camera.

Run from the project root with torch and torchvision installed.
Convolutions use symmetric int8 weights per output channel and fused batch norm.
"""
import json
import operator
import struct
from pathlib import Path
import torch
from torch import nn
from torchvision.models.segmentation import lraspp_mobilenet_v3_large, LRASPP_MobileNet_V3_Large_Weights

weights = LRASPP_MobileNet_V3_Large_Weights.DEFAULT
assert weights.meta["categories"].index("dog") == 12
model = torch.fx.symbolic_trace(lraspp_mobilenet_v3_large(weights=weights).eval())
layers, ids, payload = [], {}, bytearray()
for node in model.graph.nodes:
    if node.op == "placeholder":
        ids[node] = 0
        continue
    if node.op == "output":
        break
    if node.op == "call_module":
        module = model.get_submodule(node.target)
        if isinstance(module, nn.BatchNorm2d):
            ids[node] = ids[node.args[0]]
            continue
        layer = {"a": ids[node.args[0]], "op": type(module).__name__}
        if isinstance(module, nn.Conv2d):
            users = list(node.users)
            if len(users) == 1 and users[0].op == "call_module":
                norm = model.get_submodule(users[0].target)
                if isinstance(norm, nn.BatchNorm2d):
                    module = nn.utils.fusion.fuse_conv_bn_eval(module, norm)
            w = module.weight.detach().flatten(1)
            scale = w.abs().amax(1).clamp(min=1e-9) / 127
            bias = module.bias.detach() if module.bias is not None else torch.zeros_like(scale)
            layer.update(ch=module.out_channels, k=module.kernel_size[0],
                         stride=module.stride[0], dilation=module.dilation[0],
                         pad=module.padding[0], depth=module.groups != 1,
                         offset=len(payload), count=w.numel())
            payload.extend(scale.numpy().astype("<f4").tobytes())
            payload.extend(bias.numpy().astype("<f4").tobytes())
            payload.extend((w / scale[:, None]).round().to(torch.int8).numpy().tobytes())
        else:
            assert isinstance(module,(nn.Hardswish, nn.ReLU, nn.Hardsigmoid, nn.Sigmoid, nn.AdaptiveAvgPool2d)), type(module)
    elif node.target in (getattr, operator.getitem):
        ids[node] = ids[node.args[0]]
        continue
    elif node.target is nn.functional.interpolate:
        layer = {"op":"resize", "a":ids[node.args[0]], "b":ids[node.kwargs["size"]]}
    else:
        assert node.target in (operator.add,operator.mul)
        layer = {"op":node.target.__name__, "a":ids[node.args[0]], "b":ids[node.args[1]]}
    layers.append(layer)
    ids[node] = len(layers)
header = json.dumps(layers,separators=(",",":")).encode()
path = Path("assets/models/dog-mask.bin")
path.parent.mkdir(parents=True,exist_ok=True)
path.write_bytes(struct.pack("<I",len(header))+header+payload)
