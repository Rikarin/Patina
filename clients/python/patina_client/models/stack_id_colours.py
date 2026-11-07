from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.stack_id_colour import StackIdColour





T = TypeVar("T", bound="StackIdColours")



@_attrs_define
class StackIdColours:
    """ The colours of a stack's id map, and the one at a place on it if one was asked about.

        Attributes:
            colours (list[StackIdColour]): Its colours, the largest first.
            at (list[float] | None | Unset): The colour at the place asked about, or null.
     """

    colours: list[StackIdColour]
    at: list[float] | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_id_colour import StackIdColour # noqa: PLC0415
        colours = []
        for colours_item_data in self.colours:
            colours_item = colours_item_data.to_dict()
            colours.append(colours_item)



        at: list[float] | None | Unset
        if isinstance(self.at, Unset):
            at = UNSET
        elif isinstance(self.at, list):
            at = self.at


        else:
            at = self.at


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "colours": colours,
        })
        if at is not UNSET:
            field_dict["at"] = at

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_id_colour import StackIdColour # noqa: PLC0415
        d = dict(src_dict)
        colours = []
        _colours = d.pop("colours")
        for colours_item_data in (_colours):
            colours_item = StackIdColour.from_dict(colours_item_data)



            colours.append(colours_item)


        def _parse_at(data: object) -> list[float] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                at_type_1 = cast(list[float], data)

                return at_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[float] | None | Unset, data)

        at = _parse_at(d.pop("at", UNSET))


        stack_id_colours = cls(
            colours=colours,
            at=at,
        )


        stack_id_colours.additional_properties = d
        return stack_id_colours

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
