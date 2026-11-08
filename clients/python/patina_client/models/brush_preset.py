from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.stack_brush import StackBrush





T = TypeVar("T", bound="BrushPreset")



@_attrs_define
class BrushPreset:
    """ A brush or tool preset: what the pointer paints as and with. It is a document of a project, a `.ptbrush`
    file, and the file is the truth.

        Attributes:
            tool (str): What the pointer does: brush, eraser, line or path.
            brush (StackBrush): A brush. Everything but the radius has the value a new brush has.
            favourite (bool): Whether it is one of the favourites, which a shelf shows first. Default: False.
            colour (None | str | Unset): The colour it paints, as `#rrggbb`, or null to keep the colour chosen.
     """

    tool: str
    brush: StackBrush
    favourite: bool = False
    colour: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_brush import StackBrush # noqa: PLC0415
        tool = self.tool

        brush = self.brush.to_dict()

        favourite = self.favourite

        colour: None | str | Unset
        if isinstance(self.colour, Unset):
            colour = UNSET
        else:
            colour = self.colour


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "tool": tool,
            "brush": brush,
            "favourite": favourite,
        })
        if colour is not UNSET:
            field_dict["colour"] = colour

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_brush import StackBrush # noqa: PLC0415
        d = dict(src_dict)
        tool = d.pop("tool")

        brush = StackBrush.from_dict(d.pop("brush"))




        favourite = d.pop("favourite")

        def _parse_colour(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        colour = _parse_colour(d.pop("colour", UNSET))


        brush_preset = cls(
            tool=tool,
            brush=brush,
            favourite=favourite,
            colour=colour,
        )


        brush_preset.additional_properties = d
        return brush_preset

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
