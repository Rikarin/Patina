from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.document_cost import DocumentCost





T = TypeVar("T", bound="DocumentCostPage")



@_attrs_define
class DocumentCostPage:
    """ One page of the documents that cost more than a budget, costliest first.

        Attributes:
            items (list[DocumentCost]): The documents on this page.
            total (int): How many cost more than the budget, on all pages.
            offset (int): The offset the page starts at.
            limit (int): The most items a page holds.
     """

    items: list[DocumentCost]
    total: int
    offset: int
    limit: int
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.document_cost import DocumentCost # noqa: PLC0415
        items = []
        for items_item_data in self.items:
            items_item = items_item_data.to_dict()
            items.append(items_item)



        total = self.total

        offset = self.offset

        limit = self.limit


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "items": items,
            "total": total,
            "offset": offset,
            "limit": limit,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.document_cost import DocumentCost # noqa: PLC0415
        d = dict(src_dict)
        items = []
        _items = d.pop("items")
        for items_item_data in (_items):
            items_item = DocumentCost.from_dict(items_item_data)



            items.append(items_item)


        total = d.pop("total")

        offset = d.pop("offset")

        limit = d.pop("limit")

        document_cost_page = cls(
            items=items,
            total=total,
            offset=offset,
            limit=limit,
        )


        document_cost_page.additional_properties = d
        return document_cost_page

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
