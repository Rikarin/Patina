from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="CompressionLaneError")



@_attrs_define
class CompressionLaneError:
    """ How far one lane of a file is from what it was made from.

        Attributes:
            lane (str): r, g, b or a.
            max_error (float): The largest difference, in the file's numbers from 0 to 1.
            mean_error (float): The mean difference.
            psnr (float | None | Unset): Peak signal-to-noise ratio in decibels, or null where nothing changed.
     """

    lane: str
    max_error: float
    mean_error: float
    psnr: float | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        lane = self.lane

        max_error = self.max_error

        mean_error = self.mean_error

        psnr: float | None | Unset
        if isinstance(self.psnr, Unset):
            psnr = UNSET
        else:
            psnr = self.psnr


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "lane": lane,
            "maxError": max_error,
            "meanError": mean_error,
        })
        if psnr is not UNSET:
            field_dict["psnr"] = psnr

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        lane = d.pop("lane")

        max_error = d.pop("maxError")

        mean_error = d.pop("meanError")

        def _parse_psnr(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        psnr = _parse_psnr(d.pop("psnr", UNSET))


        compression_lane_error = cls(
            lane=lane,
            max_error=max_error,
            mean_error=mean_error,
            psnr=psnr,
        )


        compression_lane_error.additional_properties = d
        return compression_lane_error

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
