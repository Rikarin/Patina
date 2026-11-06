from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast
from uuid import UUID






T = TypeVar("T", bound="AssetSummary")



@_attrs_define
class AssetSummary:
    """ One document other projects can use, as the browser shows it.

        Attributes:
            id (UUID): The asset's identity.
            kind (str): The asset's kind, such as `smart-material`.
            name (str): The asset's display name.
            path (str): The asset's file, below the library, with forward slashes.
            tags (list[str]): The asset's tags, sorted.
            usage_count (int): How many projects reference the asset directly.
            revision (None | str | Unset): The content hash of the asset's file, or null when the file is missing.
     """

    id: UUID
    kind: str
    name: str
    path: str
    tags: list[str]
    usage_count: int
    revision: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        id = str(self.id)

        kind = self.kind

        name = self.name

        path = self.path

        tags = self.tags



        usage_count = self.usage_count

        revision: None | str | Unset
        if isinstance(self.revision, Unset):
            revision = UNSET
        else:
            revision = self.revision


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "id": id,
            "kind": kind,
            "name": name,
            "path": path,
            "tags": tags,
            "usageCount": usage_count,
        })
        if revision is not UNSET:
            field_dict["revision"] = revision

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        id = UUID(d.pop("id"))




        kind = d.pop("kind")

        name = d.pop("name")

        path = d.pop("path")

        tags = cast(list[str], d.pop("tags"))


        usage_count = d.pop("usageCount")

        def _parse_revision(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        revision = _parse_revision(d.pop("revision", UNSET))


        asset_summary = cls(
            id=id,
            kind=kind,
            name=name,
            path=path,
            tags=tags,
            usage_count=usage_count,
            revision=revision,
        )


        asset_summary.additional_properties = d
        return asset_summary

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
