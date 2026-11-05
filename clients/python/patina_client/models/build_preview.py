from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.build_preview_project import BuildPreviewProject





T = TypeVar("T", bound="BuildPreview")



@_attrs_define
class BuildPreview:
    """ What a build would do, from the build keys and with nothing run. Rebuilds and
    Pinned are exact for outputs, where the asset's impact counts every project that references
    it at all. An asset a project references and no output reads is in neither.

        Attributes:
            projects (int): Projects with something to do.
            rebuilds (int): Outputs with no result under their current key, counted once per project.
            shared (int): How many of those are the same work as another's, because their keys are equal.
            fresh (int): Outputs already built.
            pinned (int): Outputs that read the asset through a pin that holds them still.
            exports (int): Exports to write.
            exports_fresh (int): Exports already in place.
            unresolved (int): Outputs that cannot be built: a missing input, an unreadable reference.
            items (list[BuildPreviewProject]): One page of the projects, by name.
            offset (int): The offset the page starts at.
            limit (int): The most items a page holds.
     """

    projects: int
    rebuilds: int
    shared: int
    fresh: int
    pinned: int
    exports: int
    exports_fresh: int
    unresolved: int
    items: list[BuildPreviewProject]
    offset: int
    limit: int
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.build_preview_project import BuildPreviewProject # noqa: PLC0415
        projects = self.projects

        rebuilds = self.rebuilds

        shared = self.shared

        fresh = self.fresh

        pinned = self.pinned

        exports = self.exports

        exports_fresh = self.exports_fresh

        unresolved = self.unresolved

        items = []
        for items_item_data in self.items:
            items_item = items_item_data.to_dict()
            items.append(items_item)



        offset = self.offset

        limit = self.limit


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "projects": projects,
            "rebuilds": rebuilds,
            "shared": shared,
            "fresh": fresh,
            "pinned": pinned,
            "exports": exports,
            "exportsFresh": exports_fresh,
            "unresolved": unresolved,
            "items": items,
            "offset": offset,
            "limit": limit,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.build_preview_project import BuildPreviewProject # noqa: PLC0415
        d = dict(src_dict)
        projects = d.pop("projects")

        rebuilds = d.pop("rebuilds")

        shared = d.pop("shared")

        fresh = d.pop("fresh")

        pinned = d.pop("pinned")

        exports = d.pop("exports")

        exports_fresh = d.pop("exportsFresh")

        unresolved = d.pop("unresolved")

        items = []
        _items = d.pop("items")
        for items_item_data in (_items):
            items_item = BuildPreviewProject.from_dict(items_item_data)



            items.append(items_item)


        offset = d.pop("offset")

        limit = d.pop("limit")

        build_preview = cls(
            projects=projects,
            rebuilds=rebuilds,
            shared=shared,
            fresh=fresh,
            pinned=pinned,
            exports=exports,
            exports_fresh=exports_fresh,
            unresolved=unresolved,
            items=items,
            offset=offset,
            limit=limit,
        )


        build_preview.additional_properties = d
        return build_preview

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
