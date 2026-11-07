from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.unwrap_set import UnwrapSet





T = TypeVar("T", bound="UnwrapReport")



@_attrs_define
class UnwrapReport:
    """ What Auto Unwrap did to a mesh.

        Attributes:
            from_ (str): The file it unwrapped, relative to the project. Changing the options unwraps it again.
            sets (list[UnwrapSet]): One entry per texture set it laid out. A set whose material had a layout kept it and is
                absent.
     """

    from_: str
    sets: list[UnwrapSet]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.unwrap_set import UnwrapSet # noqa: PLC0415
        from_ = self.from_

        sets = []
        for sets_item_data in self.sets:
            sets_item = sets_item_data.to_dict()
            sets.append(sets_item)




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "from": from_,
            "sets": sets,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.unwrap_set import UnwrapSet # noqa: PLC0415
        d = dict(src_dict)
        from_ = d.pop("from")

        sets = []
        _sets = d.pop("sets")
        for sets_item_data in (_sets):
            sets_item = UnwrapSet.from_dict(sets_item_data)



            sets.append(sets_item)


        unwrap_report = cls(
            from_=from_,
            sets=sets,
        )


        unwrap_report.additional_properties = d
        return unwrap_report

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
