from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.stack_effect_kind import StackEffectKind
  from ..models.stack_filter_kind import StackFilterKind





T = TypeVar("T", bound="StackVocabulary")



@_attrs_define
class StackVocabulary:
    """ The words a stack's fields take, so that a client holds no list of its own.

        Attributes:
            blends (list[str]): The sixteen blend modes.
            filters (list[StackFilterKind]): The filters.
            masks (list[str]): The mask sources.
            generators (list[str]): The compounds that read a mesh's bakes.
            mesh_maps (list[str]): The maps a bake mask or a generator can read.
            alphas (list[str]): The named brush alphas.
            mask_effects (list[StackEffectKind]): The steps a mask can run.
     """

    blends: list[str]
    filters: list[StackFilterKind]
    masks: list[str]
    generators: list[str]
    mesh_maps: list[str]
    alphas: list[str]
    mask_effects: list[StackEffectKind]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_effect_kind import StackEffectKind # noqa: PLC0415
        from ..models.stack_filter_kind import StackFilterKind # noqa: PLC0415
        blends = self.blends



        filters = []
        for filters_item_data in self.filters:
            filters_item = filters_item_data.to_dict()
            filters.append(filters_item)



        masks = self.masks



        generators = self.generators



        mesh_maps = self.mesh_maps



        alphas = self.alphas



        mask_effects = []
        for mask_effects_item_data in self.mask_effects:
            mask_effects_item = mask_effects_item_data.to_dict()
            mask_effects.append(mask_effects_item)




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "blends": blends,
            "filters": filters,
            "masks": masks,
            "generators": generators,
            "meshMaps": mesh_maps,
            "alphas": alphas,
            "maskEffects": mask_effects,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_effect_kind import StackEffectKind # noqa: PLC0415
        from ..models.stack_filter_kind import StackFilterKind # noqa: PLC0415
        d = dict(src_dict)
        blends = cast(list[str], d.pop("blends"))


        filters = []
        _filters = d.pop("filters")
        for filters_item_data in (_filters):
            filters_item = StackFilterKind.from_dict(filters_item_data)



            filters.append(filters_item)


        masks = cast(list[str], d.pop("masks"))


        generators = cast(list[str], d.pop("generators"))


        mesh_maps = cast(list[str], d.pop("meshMaps"))


        alphas = cast(list[str], d.pop("alphas"))


        mask_effects = []
        _mask_effects = d.pop("maskEffects")
        for mask_effects_item_data in (_mask_effects):
            mask_effects_item = StackEffectKind.from_dict(mask_effects_item_data)



            mask_effects.append(mask_effects_item)


        stack_vocabulary = cls(
            blends=blends,
            filters=filters,
            masks=masks,
            generators=generators,
            mesh_maps=mesh_maps,
            alphas=alphas,
            mask_effects=mask_effects,
        )


        stack_vocabulary.additional_properties = d
        return stack_vocabulary

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
