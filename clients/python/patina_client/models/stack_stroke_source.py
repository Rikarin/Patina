from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.stack_picture import StackPicture





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
     """

    kind: str
    graph: None | str | Unset = UNSET
    offset: list[float] | None | Unset = UNSET
    picture: None | StackPicture | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_picture import StackPicture # noqa: PLC0415
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

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_picture import StackPicture # noqa: PLC0415
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


        stack_stroke_source = cls(
            kind=kind,
            graph=graph,
            offset=offset,
            picture=picture,
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
