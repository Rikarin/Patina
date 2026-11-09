from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="ThumbnailRequest")



@_attrs_define
class ThumbnailRequest:
    """ The body of `POST /api/v1/library/thumbnails`. A thumbnail service for every client and every list: the
    Library's grid, the shelf, and anything after them.

        Attributes:
            sources (list[str]): What to draw, each as its list names it: a document below the library (a graph, an image
                material, a layered
                material or a texturing document), a project's folder below the library (`projects/…`, drawn as the
                document that best stands for it), or a material the shelf offers from Patina itself.
     """

    sources: list[str]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        sources = self.sources




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "sources": sources,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        sources = cast(list[str], d.pop("sources"))


        thumbnail_request = cls(
            sources=sources,
        )


        thumbnail_request.additional_properties = d
        return thumbnail_request

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
