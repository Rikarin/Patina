from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.bake_options import BakeOptions





T = TypeVar("T", bound="ViewportBakePreview")



@_attrs_define
class ViewportBakePreview:
    """ A bake's setup to show in the viewport before it runs (#149): the meshes and the options a bake would be sent,
    and what of it to draw. Paths on the Server's machine, as in a bake request.

        Attributes:
            high (list[str]): The detailed meshes, in order.
            low (str): The mesh baked onto, or empty to take the preview off.
            options (BakeOptions): How a bake runs, with no meshes in it: what a project keeps between bakes and every
                client sends. Each map's
                own settings are a part of their own.
            reach (bool): Whether to draw where the rays start and how far they reach: the cage or the ray distances.
            high_mesh (bool): Whether to draw the high meshes, see-through.
            matches (bool): Whether to colour the parts by which matched which, when parts are matched by name.
            cage (None | str | Unset): A cage, or null for the ray distances.
            material (None | str | Unset): The texture set of the low mesh, or null for the whole mesh.
     """

    high: list[str]
    low: str
    options: BakeOptions
    reach: bool
    high_mesh: bool
    matches: bool
    cage: None | str | Unset = UNSET
    material: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.bake_options import BakeOptions # noqa: PLC0415
        high = self.high



        low = self.low

        options = self.options.to_dict()

        reach = self.reach

        high_mesh = self.high_mesh

        matches = self.matches

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


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "high": high,
            "low": low,
            "options": options,
            "reach": reach,
            "highMesh": high_mesh,
            "matches": matches,
        })
        if cage is not UNSET:
            field_dict["cage"] = cage
        if material is not UNSET:
            field_dict["material"] = material

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.bake_options import BakeOptions # noqa: PLC0415
        d = dict(src_dict)
        high = cast(list[str], d.pop("high"))


        low = d.pop("low")

        options = BakeOptions.from_dict(d.pop("options"))




        reach = d.pop("reach")

        high_mesh = d.pop("highMesh")

        matches = d.pop("matches")

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


        viewport_bake_preview = cls(
            high=high,
            low=low,
            options=options,
            reach=reach,
            high_mesh=high_mesh,
            matches=matches,
            cage=cage,
            material=material,
        )


        viewport_bake_preview.additional_properties = d
        return viewport_bake_preview

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
