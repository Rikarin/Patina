from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="BakeRequest")



@_attrs_define
class BakeRequest:
    """ A high-to-low bake. Everything is a path on the Server's machine, and the results are files there: a client
    sends paths and gets paths, whichever client it is (plan §8).

        Attributes:
            high (str): The detailed mesh.
            low (str): The mesh baked onto, which must have texture coordinates.
            output (str): The folder the maps are written to.
            resolution (int): The atlas's edge, in texels.
            format_ (str): `png8`, `png16`, `exr16` or `exr32`.
            supersampling (int): How many samples along each side of a texel.
            occlusion_rays (int): How many rays a texel's occlusion is estimated with.
            search_radius (float): How far a ray looks, as a fraction of the high mesh's diagonal.
            occlusion_radius (float): How far an occluder counts, as a fraction of the diagonal.
            skew (float): How far rays lean from the smoothed direction towards the low mesh's own normal, 0 to 1.
            match_by_name (bool): Whether a part of the low mesh only strikes the high part of the same name.
            gutter (int): How many texels of dilation surround each chart.
            space (str): `tangent` or `object`.
            cage (None | str | Unset): A mesh with the low mesh's faces, moved outward, which the rays start from.
            material (None | str | Unset): The material of the low mesh to bake, for one texture set of a model that has
                several.
            maps (list[str] | None | Unset): What to bake beyond `normal` and `height`, which are always baked: `ao`,
                `bent`,
                `curvature`, `thickness`, `position`, `world`, `id`.
            skew_map (None | str | Unset): A grey image saying where to apply the skew, or null for everywhere.
            name (None | str | Unset): The stem of each file's name, or null for the low mesh's.
     """

    high: str
    low: str
    output: str
    resolution: int
    format_: str
    supersampling: int
    occlusion_rays: int
    search_radius: float
    occlusion_radius: float
    skew: float
    match_by_name: bool
    gutter: int
    space: str
    cage: None | str | Unset = UNSET
    material: None | str | Unset = UNSET
    maps: list[str] | None | Unset = UNSET
    skew_map: None | str | Unset = UNSET
    name: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        high = self.high

        low = self.low

        output = self.output

        resolution = self.resolution

        format_ = self.format_

        supersampling = self.supersampling

        occlusion_rays = self.occlusion_rays

        search_radius = self.search_radius

        occlusion_radius = self.occlusion_radius

        skew = self.skew

        match_by_name = self.match_by_name

        gutter = self.gutter

        space = self.space

        cage: None | str | Unset
        if isinstance(self.cage, Unset):
            cage = UNSET
        else:
            cage = self.cage

        material: None | str | Unset
        if isinstance(self.material, Unset):
            material = UNSET
        else:
            material = self.material

        maps: list[str] | None | Unset
        if isinstance(self.maps, Unset):
            maps = UNSET
        elif isinstance(self.maps, list):
            maps = self.maps


        else:
            maps = self.maps

        skew_map: None | str | Unset
        if isinstance(self.skew_map, Unset):
            skew_map = UNSET
        else:
            skew_map = self.skew_map

        name: None | str | Unset
        if isinstance(self.name, Unset):
            name = UNSET
        else:
            name = self.name


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "high": high,
            "low": low,
            "output": output,
            "resolution": resolution,
            "format": format_,
            "supersampling": supersampling,
            "occlusionRays": occlusion_rays,
            "searchRadius": search_radius,
            "occlusionRadius": occlusion_radius,
            "skew": skew,
            "matchByName": match_by_name,
            "gutter": gutter,
            "space": space,
        })
        if cage is not UNSET:
            field_dict["cage"] = cage
        if material is not UNSET:
            field_dict["material"] = material
        if maps is not UNSET:
            field_dict["maps"] = maps
        if skew_map is not UNSET:
            field_dict["skewMap"] = skew_map
        if name is not UNSET:
            field_dict["name"] = name

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        high = d.pop("high")

        low = d.pop("low")

        output = d.pop("output")

        resolution = d.pop("resolution")

        format_ = d.pop("format")

        supersampling = d.pop("supersampling")

        occlusion_rays = d.pop("occlusionRays")

        search_radius = d.pop("searchRadius")

        occlusion_radius = d.pop("occlusionRadius")

        skew = d.pop("skew")

        match_by_name = d.pop("matchByName")

        gutter = d.pop("gutter")

        space = d.pop("space")

        def _parse_cage(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        cage = _parse_cage(d.pop("cage", UNSET))


        def _parse_material(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        material = _parse_material(d.pop("material", UNSET))


        def _parse_maps(data: object) -> list[str] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                maps_type_1 = cast(list[str], data)

                return maps_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[str] | None | Unset, data)

        maps = _parse_maps(d.pop("maps", UNSET))


        def _parse_skew_map(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        skew_map = _parse_skew_map(d.pop("skewMap", UNSET))


        def _parse_name(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        name = _parse_name(d.pop("name", UNSET))


        bake_request = cls(
            high=high,
            low=low,
            output=output,
            resolution=resolution,
            format_=format_,
            supersampling=supersampling,
            occlusion_rays=occlusion_rays,
            search_radius=search_radius,
            occlusion_radius=occlusion_radius,
            skew=skew,
            match_by_name=match_by_name,
            gutter=gutter,
            space=space,
            cage=cage,
            material=material,
            maps=maps,
            skew_map=skew_map,
            name=name,
        )


        bake_request.additional_properties = d
        return bake_request

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
