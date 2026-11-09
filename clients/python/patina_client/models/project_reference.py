from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast
from uuid import UUID






T = TypeVar("T", bound="ProjectReference")



@_attrs_define
class ProjectReference:
    """ A project's reference to an asset, as it now stands.

        Attributes:
            asset (UUID): The asset.
            pinned (bool): Whether a pin holds it.
            revision (None | str | Unset): The bytes it is pinned at, or null when it follows the asset.
     """

    asset: UUID
    pinned: bool
    revision: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        asset = str(self.asset)

        pinned = self.pinned

        revision: None | str | Unset
        if isinstance(self.revision, Unset):
            revision = UNSET
        else:
            revision = self.revision


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "asset": asset,
            "pinned": pinned,
        })
        if revision is not UNSET:
            field_dict["revision"] = revision

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        asset = UUID(d.pop("asset"))




        pinned = d.pop("pinned")

        def _parse_revision(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        revision = _parse_revision(d.pop("revision", UNSET))


        project_reference = cls(
            asset=asset,
            pinned=pinned,
            revision=revision,
        )


        project_reference.additional_properties = d
        return project_reference

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
