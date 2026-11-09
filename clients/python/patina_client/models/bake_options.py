from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.bake_colour_options import BakeColourOptions
  from ..models.bake_height_options import BakeHeightOptions
  from ..models.bake_id_options import BakeIdOptions
  from ..models.bake_occlusion_options import BakeOcclusionOptions
  from ..models.bake_ray_options import BakeRayOptions
  from ..models.bake_rounding_options import BakeRoundingOptions
  from ..models.bake_thickness_options import BakeThicknessOptions





T = TypeVar("T", bound="BakeOptions")



@_attrs_define
class BakeOptions:
    """ How a bake runs, with no meshes in it: what a project keeps between bakes and every client sends. Each map's
    own settings are a part of their own.

        Attributes:
            resolution (int): The atlas's edge, in texels.
            format_ (str): `png8`, `png16`, `exr16` or `exr32`.
            supersampling (int): How many samples along each side of a texel.
            gutter (int): How many texels of dilation surround each chart.
            maps (list[str]): Which maps to write: `normal`, `height`, `ao`, `bent`, `curvature`,
                `thickness`, `position`, `world`, `id`, `colour`. The default is what a project's
                masks read.
            space (str): The frame of the normal and bent normal maps: `tangent` or `object`.
            rays (BakeRayOptions): How far a bake's rays look from the low mesh, and what they may strike.
            height (BakeHeightOptions): The height map's stored range.
            occlusion (BakeOcclusionOptions): The occlusion rays, which ambient occlusion, the bent normal and thickness
                share.
            thickness (BakeThicknessOptions): The thickness map's own settings.
            id (BakeIdOptions): What an ID map labels, and how it colours the labels.
            rounding (BakeRoundingOptions): Edges baked round into the normal map, the bevel a hard-surface model was not
                modelled with.
            colour (BakeColourOptions): Where a colour map's colour comes from.
     """

    resolution: int
    format_: str
    supersampling: int
    gutter: int
    maps: list[str]
    space: str
    rays: BakeRayOptions
    height: BakeHeightOptions
    occlusion: BakeOcclusionOptions
    thickness: BakeThicknessOptions
    id: BakeIdOptions
    rounding: BakeRoundingOptions
    colour: BakeColourOptions
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.bake_colour_options import BakeColourOptions # noqa: PLC0415
        from ..models.bake_height_options import BakeHeightOptions # noqa: PLC0415
        from ..models.bake_id_options import BakeIdOptions # noqa: PLC0415
        from ..models.bake_occlusion_options import BakeOcclusionOptions # noqa: PLC0415
        from ..models.bake_ray_options import BakeRayOptions # noqa: PLC0415
        from ..models.bake_rounding_options import BakeRoundingOptions # noqa: PLC0415
        from ..models.bake_thickness_options import BakeThicknessOptions # noqa: PLC0415
        resolution = self.resolution

        format_ = self.format_

        supersampling = self.supersampling

        gutter = self.gutter

        maps = self.maps



        space = self.space

        rays = self.rays.to_dict()

        height = self.height.to_dict()

        occlusion = self.occlusion.to_dict()

        thickness = self.thickness.to_dict()

        id = self.id.to_dict()

        rounding = self.rounding.to_dict()

        colour = self.colour.to_dict()


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "resolution": resolution,
            "format": format_,
            "supersampling": supersampling,
            "gutter": gutter,
            "maps": maps,
            "space": space,
            "rays": rays,
            "height": height,
            "occlusion": occlusion,
            "thickness": thickness,
            "id": id,
            "rounding": rounding,
            "colour": colour,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.bake_colour_options import BakeColourOptions # noqa: PLC0415
        from ..models.bake_height_options import BakeHeightOptions # noqa: PLC0415
        from ..models.bake_id_options import BakeIdOptions # noqa: PLC0415
        from ..models.bake_occlusion_options import BakeOcclusionOptions # noqa: PLC0415
        from ..models.bake_ray_options import BakeRayOptions # noqa: PLC0415
        from ..models.bake_rounding_options import BakeRoundingOptions # noqa: PLC0415
        from ..models.bake_thickness_options import BakeThicknessOptions # noqa: PLC0415
        d = dict(src_dict)
        resolution = d.pop("resolution")

        format_ = d.pop("format")

        supersampling = d.pop("supersampling")

        gutter = d.pop("gutter")

        maps = cast(list[str], d.pop("maps"))


        space = d.pop("space")

        rays = BakeRayOptions.from_dict(d.pop("rays"))




        height = BakeHeightOptions.from_dict(d.pop("height"))




        occlusion = BakeOcclusionOptions.from_dict(d.pop("occlusion"))




        thickness = BakeThicknessOptions.from_dict(d.pop("thickness"))




        id = BakeIdOptions.from_dict(d.pop("id"))




        rounding = BakeRoundingOptions.from_dict(d.pop("rounding"))




        colour = BakeColourOptions.from_dict(d.pop("colour"))




        bake_options = cls(
            resolution=resolution,
            format_=format_,
            supersampling=supersampling,
            gutter=gutter,
            maps=maps,
            space=space,
            rays=rays,
            height=height,
            occlusion=occlusion,
            thickness=thickness,
            id=id,
            rounding=rounding,
            colour=colour,
        )


        bake_options.additional_properties = d
        return bake_options

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
