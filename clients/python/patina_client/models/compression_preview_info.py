from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.compression_lane_error import CompressionLaneError





T = TypeVar("T", bound="CompressionPreviewInfo")



@_attrs_define
class CompressionPreviewInfo:
    """ What a file's encoding does to a region of it. The pictures are not in this: each is a PNG at
    `GET /api/v1/export/compression/{id}/{picture}`, for `source`, `written` and `difference`.

        Attributes:
            id (str): The preview, by the hash of what it was made from, for its pictures.
            format_ (str): The file's format.
            compression (str): What it holds.
            level (int): The level shown.
            levels (int): How many levels the file has.
            width (int): The level's width.
            height (int): The level's height.
            region (list[int]): The region shown, as x, y, width and height in the level's texels.
            lanes (list[CompressionLaneError]): How far each lane is.
            gain (float): What the difference picture's errors are multiplied by.
            bytes_ (int): What the whole file takes in GPU memory as written, by the measure the pre-export checks use
                (`texture-over-budget`).
            uncompressed (int): What the same file would take with no block compression, by the same measure.
     """

    id: str
    format_: str
    compression: str
    level: int
    levels: int
    width: int
    height: int
    region: list[int]
    lanes: list[CompressionLaneError]
    gain: float
    bytes_: int
    uncompressed: int
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.compression_lane_error import CompressionLaneError # noqa: PLC0415
        id = self.id

        format_ = self.format_

        compression = self.compression

        level = self.level

        levels = self.levels

        width = self.width

        height = self.height

        region = self.region



        lanes = []
        for lanes_item_data in self.lanes:
            lanes_item = lanes_item_data.to_dict()
            lanes.append(lanes_item)



        gain = self.gain

        bytes_ = self.bytes_

        uncompressed = self.uncompressed


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "id": id,
            "format": format_,
            "compression": compression,
            "level": level,
            "levels": levels,
            "width": width,
            "height": height,
            "region": region,
            "lanes": lanes,
            "gain": gain,
            "bytes": bytes_,
            "uncompressed": uncompressed,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.compression_lane_error import CompressionLaneError # noqa: PLC0415
        d = dict(src_dict)
        id = d.pop("id")

        format_ = d.pop("format")

        compression = d.pop("compression")

        level = d.pop("level")

        levels = d.pop("levels")

        width = d.pop("width")

        height = d.pop("height")

        region = cast(list[int], d.pop("region"))


        lanes = []
        _lanes = d.pop("lanes")
        for lanes_item_data in (_lanes):
            lanes_item = CompressionLaneError.from_dict(lanes_item_data)



            lanes.append(lanes_item)


        gain = d.pop("gain")

        bytes_ = d.pop("bytes")

        uncompressed = d.pop("uncompressed")

        compression_preview_info = cls(
            id=id,
            format_=format_,
            compression=compression,
            level=level,
            levels=levels,
            width=width,
            height=height,
            region=region,
            lanes=lanes,
            gain=gain,
            bytes_=bytes_,
            uncompressed=uncompressed,
        )


        compression_preview_info.additional_properties = d
        return compression_preview_info

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
