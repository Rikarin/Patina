from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.folder_cost import FolderCost





T = TypeVar("T", bound="LibraryCosts")



@_attrs_define
class LibraryCosts:
    """ What a folder of the library costs, and each folder or project directly in it, costliest first (#207). A
    document's cost is its maps as an export writes them uncompressed: its size, by four bytes a texel, by a map for
    each channel of each texture set. Compression and mip levels are a preset's, and are not counted.

        Attributes:
            folder (str): The folder below `projects/`, empty for the whole library.
            documents (int): How many stacks are below it.
            bytes_ (int): What they cost together.
            items (list[FolderCost]): Each folder or project directly in it.
     """

    folder: str
    documents: int
    bytes_: int
    items: list[FolderCost]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.folder_cost import FolderCost # noqa: PLC0415
        folder = self.folder

        documents = self.documents

        bytes_ = self.bytes_

        items = []
        for items_item_data in self.items:
            items_item = items_item_data.to_dict()
            items.append(items_item)




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "folder": folder,
            "documents": documents,
            "bytes": bytes_,
            "items": items,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.folder_cost import FolderCost # noqa: PLC0415
        d = dict(src_dict)
        folder = d.pop("folder")

        documents = d.pop("documents")

        bytes_ = d.pop("bytes")

        items = []
        _items = d.pop("items")
        for items_item_data in (_items):
            items_item = FolderCost.from_dict(items_item_data)



            items.append(items_item)


        library_costs = cls(
            folder=folder,
            documents=documents,
            bytes_=bytes_,
            items=items,
        )


        library_costs.additional_properties = d
        return library_costs

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
