from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.stack_layer_settings import StackLayerSettings
  from ..models.stack_layer_textures import StackLayerTextures
  from ..models.stack_layer_values import StackLayerValues
  from ..models.stack_mask import StackMask





T = TypeVar("T", bound="StackLayer")



@_attrs_define
class StackLayer:
    """ One layer, without its pixels.

        Attributes:
            id (str): Its identifier, stable across renames.
            name (str): What it is called.
            kind (str): fill, paint, filter or group.
            enabled (bool): Whether it composites.
            opacity (float): How much of it composites.
            blend (str): Its blend mode.
            channels (list[str]): Which channels it writes. Empty means all of them.
            painted (bool): Whether it names a paint file yet.
            children (list[StackLayer]): What a group holds.
            fill (str): What a fill reads: Constant, Texture or Graph.
            values (StackLayerValues): What a constant fill writes to each channel.
            textures (StackLayerTextures): What a texture fill reads for each channel.
            graph (str): The graph a graph fill reads.
            projection (str): How a fill lands on the mesh: Uv, Triplanar or Planar.
            filter_ (str): What a filter does: Levels, Hsl, Blur, Invert or Grayscale.
            settings (StackLayerSettings): A filter's numbers.
            mask (StackMask): What decides where a layer shows.
     """

    id: str
    name: str
    kind: str
    enabled: bool
    opacity: float
    blend: str
    channels: list[str]
    painted: bool
    children: list[StackLayer]
    fill: str
    values: StackLayerValues
    textures: StackLayerTextures
    graph: str
    projection: str
    filter_: str
    settings: StackLayerSettings
    mask: StackMask
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_layer_settings import StackLayerSettings # noqa: PLC0415
        from ..models.stack_layer_textures import StackLayerTextures # noqa: PLC0415
        from ..models.stack_layer_values import StackLayerValues # noqa: PLC0415
        from ..models.stack_mask import StackMask # noqa: PLC0415
        id = self.id

        name = self.name

        kind = self.kind

        enabled = self.enabled

        opacity = self.opacity

        blend = self.blend

        channels = self.channels



        painted = self.painted

        children = []
        for children_item_data in self.children:
            children_item = children_item_data.to_dict()
            children.append(children_item)



        fill = self.fill

        values = self.values.to_dict()

        textures = self.textures.to_dict()

        graph = self.graph

        projection = self.projection

        filter_ = self.filter_

        settings = self.settings.to_dict()

        mask = self.mask.to_dict()


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "id": id,
            "name": name,
            "kind": kind,
            "enabled": enabled,
            "opacity": opacity,
            "blend": blend,
            "channels": channels,
            "painted": painted,
            "children": children,
            "fill": fill,
            "values": values,
            "textures": textures,
            "graph": graph,
            "projection": projection,
            "filter": filter_,
            "settings": settings,
            "mask": mask,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_layer_settings import StackLayerSettings # noqa: PLC0415
        from ..models.stack_layer_textures import StackLayerTextures # noqa: PLC0415
        from ..models.stack_layer_values import StackLayerValues # noqa: PLC0415
        from ..models.stack_mask import StackMask # noqa: PLC0415
        d = dict(src_dict)
        id = d.pop("id")

        name = d.pop("name")

        kind = d.pop("kind")

        enabled = d.pop("enabled")

        opacity = d.pop("opacity")

        blend = d.pop("blend")

        channels = cast(list[str], d.pop("channels"))


        painted = d.pop("painted")

        children = []
        _children = d.pop("children")
        for children_item_data in (_children):
            children_item = StackLayer.from_dict(children_item_data)



            children.append(children_item)


        fill = d.pop("fill")

        values = StackLayerValues.from_dict(d.pop("values"))




        textures = StackLayerTextures.from_dict(d.pop("textures"))




        graph = d.pop("graph")

        projection = d.pop("projection")

        filter_ = d.pop("filter")

        settings = StackLayerSettings.from_dict(d.pop("settings"))




        mask = StackMask.from_dict(d.pop("mask"))




        stack_layer = cls(
            id=id,
            name=name,
            kind=kind,
            enabled=enabled,
            opacity=opacity,
            blend=blend,
            channels=channels,
            painted=painted,
            children=children,
            fill=fill,
            values=values,
            textures=textures,
            graph=graph,
            projection=projection,
            filter_=filter_,
            settings=settings,
            mask=mask,
        )


        stack_layer.additional_properties = d
        return stack_layer

    @property
    def additional_keys(self) -> list[str]:
        return list(self.additional_properties.keys())

    def __getitem__(self, key: str) -> Any:
        return self.additional_properties[key]

    def __setitem__(self, key: str, value: Any) -> None:
        self.additional_properties[key] = value

    def __delitem__(self, key: str) -> None:
        del self.additional_properties[key]

    def __contains__(self, key: str) -> bool:
        return key in self.additional_properties
