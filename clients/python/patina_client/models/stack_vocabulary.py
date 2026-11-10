from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.stack_brush_terms import StackBrushTerms
  from ..models.stack_filter_kind import StackFilterKind
  from ..models.stack_term import StackTerm
  from ..models.stack_vocabulary_generator_parameters import StackVocabularyGeneratorParameters





T = TypeVar("T", bound="StackVocabulary")



@_attrs_define
class StackVocabulary:
    """ The words a stack's fields take, each with its label, so that a client holds no list of its own.

        Attributes:
            blends (list[StackTerm]): The thirty blend modes.
            filters (list[StackFilterKind]): The filters a layer can be.
            masks (list[StackTerm]): The mask sources.
            fills (list[StackTerm]): What a fill layer reads.
            projections (list[StackTerm]): How a fill's pictures land on the mesh.
            generators (list[StackTerm]): The compounds that read a mesh's bakes.
            mesh_maps (list[StackTerm]): The maps a bake mask or a generator can read.
            alphas (list[StackTerm]): The named brush alphas.
            mask_effects (list[StackFilterKind]): The steps a mask can run.
            usages (list[StackTerm]): The channels a stack can have.
            generator_parameters (StackVocabularyGeneratorParameters): The knobs each generator has, by its path.
            brush (StackBrushTerms): What a brush's fields take and how a new brush starts, so a client keeps no list or
                default of its own.
            inputs (list[StackTerm]): Where an image input can read: the layers beneath, an anchor point, a generator, an
                image or a
                mesh map.
            sources (list[StackFilterKind]): The generators an image input can read: noises, patterns, grunges and the rest.
            lanes (list[StackTerm] | None | Unset): The lanes of a picture a mask can read.
            tags (list[StackTerm] | None | Unset): The colours a layer can be marked with, None first.
     """

    blends: list[StackTerm]
    filters: list[StackFilterKind]
    masks: list[StackTerm]
    fills: list[StackTerm]
    projections: list[StackTerm]
    generators: list[StackTerm]
    mesh_maps: list[StackTerm]
    alphas: list[StackTerm]
    mask_effects: list[StackFilterKind]
    usages: list[StackTerm]
    generator_parameters: StackVocabularyGeneratorParameters
    brush: StackBrushTerms
    inputs: list[StackTerm]
    sources: list[StackFilterKind]
    lanes: list[StackTerm] | None | Unset = UNSET
    tags: list[StackTerm] | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_brush_terms import StackBrushTerms # noqa: PLC0415
        from ..models.stack_filter_kind import StackFilterKind # noqa: PLC0415
        from ..models.stack_term import StackTerm # noqa: PLC0415
        from ..models.stack_vocabulary_generator_parameters import StackVocabularyGeneratorParameters # noqa: PLC0415
        blends = []
        for blends_item_data in self.blends:
            blends_item = blends_item_data.to_dict()
            blends.append(blends_item)



        filters = []
        for filters_item_data in self.filters:
            filters_item = filters_item_data.to_dict()
            filters.append(filters_item)



        masks = []
        for masks_item_data in self.masks:
            masks_item = masks_item_data.to_dict()
            masks.append(masks_item)



        fills = []
        for fills_item_data in self.fills:
            fills_item = fills_item_data.to_dict()
            fills.append(fills_item)



        projections = []
        for projections_item_data in self.projections:
            projections_item = projections_item_data.to_dict()
            projections.append(projections_item)



        generators = []
        for generators_item_data in self.generators:
            generators_item = generators_item_data.to_dict()
            generators.append(generators_item)



        mesh_maps = []
        for mesh_maps_item_data in self.mesh_maps:
            mesh_maps_item = mesh_maps_item_data.to_dict()
            mesh_maps.append(mesh_maps_item)



        alphas = []
        for alphas_item_data in self.alphas:
            alphas_item = alphas_item_data.to_dict()
            alphas.append(alphas_item)



        mask_effects = []
        for mask_effects_item_data in self.mask_effects:
            mask_effects_item = mask_effects_item_data.to_dict()
            mask_effects.append(mask_effects_item)



        usages = []
        for usages_item_data in self.usages:
            usages_item = usages_item_data.to_dict()
            usages.append(usages_item)



        generator_parameters = self.generator_parameters.to_dict()

        brush = self.brush.to_dict()

        inputs = []
        for inputs_item_data in self.inputs:
            inputs_item = inputs_item_data.to_dict()
            inputs.append(inputs_item)



        sources = []
        for sources_item_data in self.sources:
            sources_item = sources_item_data.to_dict()
            sources.append(sources_item)



        lanes: list[dict[str, Any]] | None | Unset
        if isinstance(self.lanes, Unset):
            lanes = UNSET
        elif isinstance(self.lanes, list):
            lanes = []
            for lanes_type_1_item_data in self.lanes:
                lanes_type_1_item = lanes_type_1_item_data.to_dict()
                lanes.append(lanes_type_1_item)


        else:
            lanes = self.lanes

        tags: list[dict[str, Any]] | None | Unset
        if isinstance(self.tags, Unset):
            tags = UNSET
        elif isinstance(self.tags, list):
            tags = []
            for tags_type_1_item_data in self.tags:
                tags_type_1_item = tags_type_1_item_data.to_dict()
                tags.append(tags_type_1_item)


        else:
            tags = self.tags


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "blends": blends,
            "filters": filters,
            "masks": masks,
            "fills": fills,
            "projections": projections,
            "generators": generators,
            "meshMaps": mesh_maps,
            "alphas": alphas,
            "maskEffects": mask_effects,
            "usages": usages,
            "generatorParameters": generator_parameters,
            "brush": brush,
            "inputs": inputs,
            "sources": sources,
        })
        if lanes is not UNSET:
            field_dict["lanes"] = lanes
        if tags is not UNSET:
            field_dict["tags"] = tags

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_brush_terms import StackBrushTerms # noqa: PLC0415
        from ..models.stack_filter_kind import StackFilterKind # noqa: PLC0415
        from ..models.stack_term import StackTerm # noqa: PLC0415
        from ..models.stack_vocabulary_generator_parameters import StackVocabularyGeneratorParameters # noqa: PLC0415
        d = dict(src_dict)
        blends = []
        _blends = d.pop("blends")
        for blends_item_data in (_blends):
            blends_item = StackTerm.from_dict(blends_item_data)



            blends.append(blends_item)


        filters = []
        _filters = d.pop("filters")
        for filters_item_data in (_filters):
            filters_item = StackFilterKind.from_dict(filters_item_data)



            filters.append(filters_item)


        masks = []
        _masks = d.pop("masks")
        for masks_item_data in (_masks):
            masks_item = StackTerm.from_dict(masks_item_data)



            masks.append(masks_item)


        fills = []
        _fills = d.pop("fills")
        for fills_item_data in (_fills):
            fills_item = StackTerm.from_dict(fills_item_data)



            fills.append(fills_item)


        projections = []
        _projections = d.pop("projections")
        for projections_item_data in (_projections):
            projections_item = StackTerm.from_dict(projections_item_data)



            projections.append(projections_item)


        generators = []
        _generators = d.pop("generators")
        for generators_item_data in (_generators):
            generators_item = StackTerm.from_dict(generators_item_data)



            generators.append(generators_item)


        mesh_maps = []
        _mesh_maps = d.pop("meshMaps")
        for mesh_maps_item_data in (_mesh_maps):
            mesh_maps_item = StackTerm.from_dict(mesh_maps_item_data)



            mesh_maps.append(mesh_maps_item)


        alphas = []
        _alphas = d.pop("alphas")
        for alphas_item_data in (_alphas):
            alphas_item = StackTerm.from_dict(alphas_item_data)



            alphas.append(alphas_item)


        mask_effects = []
        _mask_effects = d.pop("maskEffects")
        for mask_effects_item_data in (_mask_effects):
            mask_effects_item = StackFilterKind.from_dict(mask_effects_item_data)



            mask_effects.append(mask_effects_item)


        usages = []
        _usages = d.pop("usages")
        for usages_item_data in (_usages):
            usages_item = StackTerm.from_dict(usages_item_data)



            usages.append(usages_item)


        generator_parameters = StackVocabularyGeneratorParameters.from_dict(d.pop("generatorParameters"))




        brush = StackBrushTerms.from_dict(d.pop("brush"))




        inputs = []
        _inputs = d.pop("inputs")
        for inputs_item_data in (_inputs):
            inputs_item = StackTerm.from_dict(inputs_item_data)



            inputs.append(inputs_item)


        sources = []
        _sources = d.pop("sources")
        for sources_item_data in (_sources):
            sources_item = StackFilterKind.from_dict(sources_item_data)



            sources.append(sources_item)


        def _parse_lanes(data: object) -> list[StackTerm] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                lanes_type_1 = []
                _lanes_type_1 = data
                for lanes_type_1_item_data in (_lanes_type_1):
                    lanes_type_1_item = StackTerm.from_dict(lanes_type_1_item_data)



                    lanes_type_1.append(lanes_type_1_item)

                return lanes_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[StackTerm] | None | Unset, data)

        lanes = _parse_lanes(d.pop("lanes", UNSET))


        def _parse_tags(data: object) -> list[StackTerm] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                tags_type_1 = []
                _tags_type_1 = data
                for tags_type_1_item_data in (_tags_type_1):
                    tags_type_1_item = StackTerm.from_dict(tags_type_1_item_data)



                    tags_type_1.append(tags_type_1_item)

                return tags_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[StackTerm] | None | Unset, data)

        tags = _parse_tags(d.pop("tags", UNSET))


        stack_vocabulary = cls(
            blends=blends,
            filters=filters,
            masks=masks,
            fills=fills,
            projections=projections,
            generators=generators,
            mesh_maps=mesh_maps,
            alphas=alphas,
            mask_effects=mask_effects,
            usages=usages,
            generator_parameters=generator_parameters,
            brush=brush,
            inputs=inputs,
            sources=sources,
            lanes=lanes,
            tags=tags,
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
