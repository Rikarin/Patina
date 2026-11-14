from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="UnwrapOptions")



@_attrs_define
class UnwrapOptions:
    """ How Auto Unwrap lays out a mesh. Each texture set gets an atlas of its own.

        Attributes:
            resolution (int): The atlas side the margin is counted against, in texels. A map of another size only rescales
                it.
            margin (int): The empty texels between two islands.
            quality (str): How islands are packed: `boxes` by their bounding boxes, `outlines` by their shapes, and
                `groups` by their shapes after joining nearly square neighbours.
            rotations (int): How many ways an island may be turned: 1 never, 2 a half turn, 4 every quarter turn.
            texel_density (float): Texels per world unit every island is scaled to, or zero for each island at its own
                scale.
            overflow (str): What happens when the islands do not fit at that density: `shrink` scales them all down, and
                `refuse` stops and says so. Spilling into a second UDIM tile is not offered while a texture set
                paints one tile.
     """

    resolution: int
    margin: int
    quality: str
    rotations: int
    texel_density: float
    overflow: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        resolution = self.resolution

        margin = self.margin

        quality = self.quality

        rotations = self.rotations

        texel_density = self.texel_density

        overflow = self.overflow


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "resolution": resolution,
            "margin": margin,
            "quality": quality,
            "rotations": rotations,
            "texelDensity": texel_density,
            "overflow": overflow,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        resolution = d.pop("resolution")

        margin = d.pop("margin")

        quality = d.pop("quality")

        rotations = d.pop("rotations")

        texel_density = d.pop("texelDensity")

        overflow = d.pop("overflow")

        unwrap_options = cls(
            resolution=resolution,
            margin=margin,
            quality=quality,
            rotations=rotations,
            texel_density=texel_density,
            overflow=overflow,
        )


        unwrap_options.additional_properties = d
        return unwrap_options

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
