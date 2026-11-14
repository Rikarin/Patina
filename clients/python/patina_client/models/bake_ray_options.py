from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="BakeRayOptions")



@_attrs_define
class BakeRayOptions:
    """ How far a bake's rays look from the low mesh, and what they may strike.

        Attributes:
            front (float): How far a ray looks outward, in front of the low mesh.
            rear (float): How far a ray looks inward, behind the low mesh, or past it from a cage.
            units (str): What the distances here and in BakeOcclusionOptions are in: `relative`, fractions of the
                high mesh's bounding-box diagonal, or `absolute`, the model's own units.
            ignore_backfaces (bool): Whether a ray passes through a high face turned away from the low mesh's facing.
            automatic_cage (bool): Whether, with no cage file, the rays start on the low mesh pushed out by the front
                distance and take the
                first surface they meet, as from a cage, rather than the nearer surface either way (#148).
            skew (float): How far rays lean from the smoothed direction towards the low mesh's own normal, 0 to 1.
            skew_map (str): A grey image saying where to apply the skew, or empty for everywhere.
            distance_map (str): A grey image correcting how far the rays reach over the atlas, or empty for as set: mid grey
                leaves them,
                black pulls them onto the low mesh and white sends them twice as far, front, rear and cage alike.
            match_by_name (bool): Whether a part of the low mesh only strikes the high part of the same name.
            low_suffix (str): What a low part's name ends in, which matching by name takes off.
            high_suffix (str): What a high part's name ends in, which matching by name takes off.
     """

    front: float
    rear: float
    units: str
    ignore_backfaces: bool
    automatic_cage: bool
    skew: float
    skew_map: str
    distance_map: str
    match_by_name: bool
    low_suffix: str
    high_suffix: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        front = self.front

        rear = self.rear

        units = self.units

        ignore_backfaces = self.ignore_backfaces

        automatic_cage = self.automatic_cage

        skew = self.skew

        skew_map = self.skew_map

        distance_map = self.distance_map

        match_by_name = self.match_by_name

        low_suffix = self.low_suffix

        high_suffix = self.high_suffix


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "front": front,
            "rear": rear,
            "units": units,
            "ignoreBackfaces": ignore_backfaces,
            "automaticCage": automatic_cage,
            "skew": skew,
            "skewMap": skew_map,
            "distanceMap": distance_map,
            "matchByName": match_by_name,
            "lowSuffix": low_suffix,
            "highSuffix": high_suffix,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        front = d.pop("front")

        rear = d.pop("rear")

        units = d.pop("units")

        ignore_backfaces = d.pop("ignoreBackfaces")

        automatic_cage = d.pop("automaticCage")

        skew = d.pop("skew")

        skew_map = d.pop("skewMap")

        distance_map = d.pop("distanceMap")

        match_by_name = d.pop("matchByName")

        low_suffix = d.pop("lowSuffix")

        high_suffix = d.pop("highSuffix")

        bake_ray_options = cls(
            front=front,
            rear=rear,
            units=units,
            ignore_backfaces=ignore_backfaces,
            automatic_cage=automatic_cage,
            skew=skew,
            skew_map=skew_map,
            distance_map=distance_map,
            match_by_name=match_by_name,
            low_suffix=low_suffix,
            high_suffix=high_suffix,
        )


        bake_ray_options.additional_properties = d
        return bake_ray_options

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
