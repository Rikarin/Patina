from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.asset_summary import AssetSummary
  from ..models.impacted_project import ImpactedProject





T = TypeVar("T", bound="AssetImpact")



@_attrs_define
class AssetImpact:
    """ What saving a shared asset would touch: "affects 412 projects, 3 pinned" (plan §5).

        Attributes:
            asset (AssetSummary): One document other projects can use, as the browser shows it.
            projects (int): The projects that depend on the asset, directly or through other assets.
            pinned (int): How many of Projects a pin holds back; the rest rebuild.
            unreadable_documents (int): Project and asset documents the index could not read. They may depend on the asset
                too, so a value above zero
                means the counts are a lower bound.
            items (list[ImpactedProject]): One page of the dependent projects.
            offset (int): The offset the page starts at.
            limit (int): The most items a page holds.
     """

    asset: AssetSummary
    projects: int
    pinned: int
    unreadable_documents: int
    items: list[ImpactedProject]
    offset: int
    limit: int
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.asset_summary import AssetSummary # noqa: PLC0415
        from ..models.impacted_project import ImpactedProject # noqa: PLC0415
        asset = self.asset.to_dict()

        projects = self.projects

        pinned = self.pinned

        unreadable_documents = self.unreadable_documents

        items = []
        for items_item_data in self.items:
            items_item = items_item_data.to_dict()
            items.append(items_item)



        offset = self.offset

        limit = self.limit


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "asset": asset,
            "projects": projects,
            "pinned": pinned,
            "unreadableDocuments": unreadable_documents,
            "items": items,
            "offset": offset,
            "limit": limit,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.asset_summary import AssetSummary # noqa: PLC0415
        from ..models.impacted_project import ImpactedProject # noqa: PLC0415
        d = dict(src_dict)
        asset = AssetSummary.from_dict(d.pop("asset"))




        projects = d.pop("projects")

        pinned = d.pop("pinned")

        unreadable_documents = d.pop("unreadableDocuments")

        items = []
        _items = d.pop("items")
        for items_item_data in (_items):
            items_item = ImpactedProject.from_dict(items_item_data)



            items.append(items_item)


        offset = d.pop("offset")

        limit = d.pop("limit")

        asset_impact = cls(
            asset=asset,
            projects=projects,
            pinned=pinned,
            unreadable_documents=unreadable_documents,
            items=items,
            offset=offset,
            limit=limit,
        )


        asset_impact.additional_properties = d
        return asset_impact

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
