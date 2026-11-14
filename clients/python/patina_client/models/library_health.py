from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.library_finding_group import LibraryFindingGroup





T = TypeVar("T", bound="LibraryHealth")



@_attrs_define
class LibraryHealth:
    """ The health of a library, from `POST /api/v1/library/check`.

        Attributes:
            projects (int): The projects checked.
            assets (int): The shared assets checked.
            outputs (int): The outputs checked.
            errors (int): How many findings say the library is wrong.
            groups (list[LibraryFindingGroup]): The findings by kind, errors first.
     """

    projects: int
    assets: int
    outputs: int
    errors: int
    groups: list[LibraryFindingGroup]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.library_finding_group import LibraryFindingGroup # noqa: PLC0415
        projects = self.projects

        assets = self.assets

        outputs = self.outputs

        errors = self.errors

        groups = []
        for groups_item_data in self.groups:
            groups_item = groups_item_data.to_dict()
            groups.append(groups_item)




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "projects": projects,
            "assets": assets,
            "outputs": outputs,
            "errors": errors,
            "groups": groups,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.library_finding_group import LibraryFindingGroup # noqa: PLC0415
        d = dict(src_dict)
        projects = d.pop("projects")

        assets = d.pop("assets")

        outputs = d.pop("outputs")

        errors = d.pop("errors")

        groups = []
        _groups = d.pop("groups")
        for groups_item_data in (_groups):
            groups_item = LibraryFindingGroup.from_dict(groups_item_data)



            groups.append(groups_item)


        library_health = cls(
            projects=projects,
            assets=assets,
            outputs=outputs,
            errors=errors,
            groups=groups,
        )


        library_health.additional_properties = d
        return library_health

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
