from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.material_info import MaterialInfo
  from ..models.stack_term import StackTerm





T = TypeVar("T", bound="MaterialList")



@_attrs_define
class MaterialList:
    """ The materials a project can use.

        Attributes:
            items (list[MaterialInfo]): Them, every one with each word of the search, if there was one.
            roles (list[StackTerm]): The types a shelf can narrow to, with what to call them.
            origins (list[StackTerm]): Where a material can be from, with what to call each.
     """

    items: list[MaterialInfo]
    roles: list[StackTerm]
    origins: list[StackTerm]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.material_info import MaterialInfo # noqa: PLC0415
        from ..models.stack_term import StackTerm # noqa: PLC0415
        items = []
        for items_item_data in self.items:
            items_item = items_item_data.to_dict()
            items.append(items_item)



        roles = []
        for roles_item_data in self.roles:
            roles_item = roles_item_data.to_dict()
            roles.append(roles_item)



        origins = []
        for origins_item_data in self.origins:
            origins_item = origins_item_data.to_dict()
            origins.append(origins_item)




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "items": items,
            "roles": roles,
            "origins": origins,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.material_info import MaterialInfo # noqa: PLC0415
        from ..models.stack_term import StackTerm # noqa: PLC0415
        d = dict(src_dict)
        items = []
        _items = d.pop("items")
        for items_item_data in (_items):
            items_item = MaterialInfo.from_dict(items_item_data)



            items.append(items_item)


        roles = []
        _roles = d.pop("roles")
        for roles_item_data in (_roles):
            roles_item = StackTerm.from_dict(roles_item_data)



            roles.append(roles_item)


        origins = []
        _origins = d.pop("origins")
        for origins_item_data in (_origins):
            origins_item = StackTerm.from_dict(origins_item_data)



            origins.append(origins_item)


        material_list = cls(
            items=items,
            roles=roles,
            origins=origins,
        )


        material_list.additional_properties = d
        return material_list

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
