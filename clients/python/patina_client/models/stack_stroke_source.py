from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.stack_frame import StackFrame
  from ..models.stack_picture import StackPicture
  from ..models.stack_placement import StackPlacement





T = TypeVar("T", bound="StackStrokeSource")



@_attrs_define
class StackStrokeSource:
    """ Where a stroke's colour comes from, when it is not one colour.

        Attributes:
            kind (str): `smudge` drags what the canvas holds along the stroke, with the brush's flow; `clone` copies the
                            stack's composite from Offset away, as it stood when the stroke began; `material` lays
                            what Graph makes in the stroke's channel; `picture` projects
                            Picture.
            graph (None | str | Unset): For a material, the graph whose maps are laid, as a graph fill names it.
            offset (list[float] | None | Unset): For a clone, how far the texel read is from the texel painted, in texels of
                the map: two numbers.
            picture (None | StackPicture | Unset):
            projection (None | str | Unset): For a material, how its maps land on the mesh, as a fill's do: `Uv` (the
                default) lays them over the
                layout, so a material's size follows how the UVs are laid out; `Triplanar`, `Planar`,
                `Spherical` and `Cylindrical` lay them in the model's space, the same size wherever the stroke goes.
                A projection in the model's space needs a mesh.
            planar_axis (None | str | Unset): For a planar material, the axis it projects along: X, Y or Z.
            placement (None | StackPlacement | Unset):
            frame (None | StackFrame | Unset):
     """

    kind: str
    graph: None | str | Unset = UNSET
    offset: list[float] | None | Unset = UNSET
    picture: None | StackPicture | Unset = UNSET
    projection: None | str | Unset = UNSET
    planar_axis: None | str | Unset = UNSET
    placement: None | StackPlacement | Unset = UNSET
    frame: None | StackFrame | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_frame import StackFrame # noqa: PLC0415
        from ..models.stack_picture import StackPicture # noqa: PLC0415
        from ..models.stack_placement import StackPlacement # noqa: PLC0415
        kind = self.kind

        graph: None | str | Unset
        if isinstance(self.graph, Unset):
            graph = UNSET
        else:
            graph = self.graph

        offset: list[float] | None | Unset
        if isinstance(self.offset, Unset):
            offset = UNSET
        elif isinstance(self.offset, list):
            offset = self.offset


        else:
            offset = self.offset

        picture: dict[str, Any] | None | Unset
        if isinstance(self.picture, Unset):
            picture = UNSET
        elif isinstance(self.picture, StackPicture):
            picture = self.picture.to_dict()
        else:
            picture = self.picture

        projection: None | str | Unset
        if isinstance(self.projection, Unset):
            projection = UNSET
        else:
            projection = self.projection

        planar_axis: None | str | Unset
        if isinstance(self.planar_axis, Unset):
            planar_axis = UNSET
        else:
            planar_axis = self.planar_axis

        placement: dict[str, Any] | None | Unset
        if isinstance(self.placement, Unset):
            placement = UNSET
        elif isinstance(self.placement, StackPlacement):
            placement = self.placement.to_dict()
        else:
            placement = self.placement

        frame: dict[str, Any] | None | Unset
        if isinstance(self.frame, Unset):
            frame = UNSET
        elif isinstance(self.frame, StackFrame):
            frame = self.frame.to_dict()
        else:
            frame = self.frame


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "kind": kind,
        })
        if graph is not UNSET:
            field_dict["graph"] = graph
        if offset is not UNSET:
            field_dict["offset"] = offset
        if picture is not UNSET:
            field_dict["picture"] = picture
        if projection is not UNSET:
            field_dict["projection"] = projection
        if planar_axis is not UNSET:
            field_dict["planarAxis"] = planar_axis
        if placement is not UNSET:
            field_dict["placement"] = placement
        if frame is not UNSET:
            field_dict["frame"] = frame

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_frame import StackFrame # noqa: PLC0415
        from ..models.stack_picture import StackPicture # noqa: PLC0415
        from ..models.stack_placement import StackPlacement # noqa: PLC0415
        d = dict(src_dict)
        kind = d.pop("kind")

        def _parse_graph(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        graph = _parse_graph(d.pop("graph", UNSET))


        def _parse_offset(data: object) -> list[float] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                offset_type_1 = cast(list[float], data)

                return offset_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[float] | None | Unset, data)

        offset = _parse_offset(d.pop("offset", UNSET))


        def _parse_picture(data: object) -> None | StackPicture | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                picture_type_1 = StackPicture.from_dict(data)



                return picture_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackPicture | Unset, data)

        picture = _parse_picture(d.pop("picture", UNSET))


        def _parse_projection(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        projection = _parse_projection(d.pop("projection", UNSET))


        def _parse_planar_axis(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        planar_axis = _parse_planar_axis(d.pop("planarAxis", UNSET))


        def _parse_placement(data: object) -> None | StackPlacement | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                placement_type_1 = StackPlacement.from_dict(data)



                return placement_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackPlacement | Unset, data)

        placement = _parse_placement(d.pop("placement", UNSET))


        def _parse_frame(data: object) -> None | StackFrame | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                frame_type_1 = StackFrame.from_dict(data)



                return frame_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackFrame | Unset, data)

        frame = _parse_frame(d.pop("frame", UNSET))


        stack_stroke_source = cls(
            kind=kind,
            graph=graph,
            offset=offset,
            picture=picture,
            projection=projection,
            planar_axis=planar_axis,
            placement=placement,
            frame=frame,
        )


        stack_stroke_source.additional_properties = d
        return stack_stroke_source

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
