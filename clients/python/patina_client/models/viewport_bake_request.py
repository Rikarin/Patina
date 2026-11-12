from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="ViewportBakeRequest")



@_attrs_define
class ViewportBakeRequest:
    """ Asks the viewport to show a finished bake on the mesh it was baked onto.

        Attributes:
            job (str): The bake's job.
            map_ (None | str | Unset): One of its maps to show alone on the mesh as stored, through the picture view, such
                as `normal` or
                `ao`; or null for the normal and occlusion maps on the mesh's material (#149).
     """

    job: str
    map_: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        job = self.job

        map_: None | str | Unset
        if isinstance(self.map_, Unset):
            map_ = UNSET
        else:
            map_ = self.map_


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "job": job,
        })
        if map_ is not UNSET:
            field_dict["map"] = map_

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        job = d.pop("job")

        def _parse_map_(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        map_ = _parse_map_(d.pop("map", UNSET))


        viewport_bake_request = cls(
            job=job,
            map_=map_,
        )


        viewport_bake_request.additional_properties = d
        return viewport_bake_request

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
