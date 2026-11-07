from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.project_report import ProjectReport





T = TypeVar("T", bound="ProjectReportPage")



@_attrs_define
class ProjectReportPage:
    """ A page of a build's per-project reports.

        Attributes:
            total (int): How many there are.
            items (list[ProjectReport]): This page.
            offset (int): The offset the page starts at.
            limit (int): The most items a page holds.
     """

    total: int
    items: list[ProjectReport]
    offset: int
    limit: int
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.project_report import ProjectReport # noqa: PLC0415
        total = self.total

        items = []
        for items_item_data in self.items:
            items_item = items_item_data.to_dict()
            items.append(items_item)



        offset = self.offset

        limit = self.limit


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "total": total,
            "items": items,
            "offset": offset,
            "limit": limit,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.project_report import ProjectReport # noqa: PLC0415
        d = dict(src_dict)
        total = d.pop("total")

        items = []
        _items = d.pop("items")
        for items_item_data in (_items):
            items_item = ProjectReport.from_dict(items_item_data)



            items.append(items_item)


        offset = d.pop("offset")

        limit = d.pop("limit")

        project_report_page = cls(
            total=total,
            items=items,
            offset=offset,
            limit=limit,
        )


        project_report_page.additional_properties = d
        return project_report_page

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
