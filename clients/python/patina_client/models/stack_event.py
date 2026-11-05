from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.graph_event_kind import GraphEventKind
from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.graph_history_state import GraphHistoryState
  from ..models.stack_delta import StackDelta
  from ..models.stack_evaluation import StackEvaluation





T = TypeVar("T", bound="StackEvent")



@_attrs_define
class StackEvent:
    """ Something that happened to a stack, with the same kinds and numbering as a graph's events.

        Attributes:
            sequence (int): Its number.
            kind (GraphEventKind): What an event reports.
            revision (int): The revision of the document after it.
            history (GraphHistoryState): What the undo history offers.
            label (None | str | Unset): What the undo step says.
            delta (None | StackDelta | Unset):
            evaluation (None | StackEvaluation | Unset):
     """

    sequence: int
    kind: GraphEventKind
    revision: int
    history: GraphHistoryState
    label: None | str | Unset = UNSET
    delta: None | StackDelta | Unset = UNSET
    evaluation: None | StackEvaluation | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.graph_history_state import GraphHistoryState # noqa: PLC0415
        from ..models.stack_delta import StackDelta # noqa: PLC0415
        from ..models.stack_evaluation import StackEvaluation # noqa: PLC0415
        sequence = self.sequence

        kind = self.kind.value

        revision = self.revision

        history = self.history.to_dict()

        label: None | str | Unset
        if isinstance(self.label, Unset):
            label = UNSET
        else:
            label = self.label

        delta: dict[str, Any] | None | Unset
        if isinstance(self.delta, Unset):
            delta = UNSET
        elif isinstance(self.delta, StackDelta):
            delta = self.delta.to_dict()
        else:
            delta = self.delta

        evaluation: dict[str, Any] | None | Unset
        if isinstance(self.evaluation, Unset):
            evaluation = UNSET
        elif isinstance(self.evaluation, StackEvaluation):
            evaluation = self.evaluation.to_dict()
        else:
            evaluation = self.evaluation


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "sequence": sequence,
            "kind": kind,
            "revision": revision,
            "history": history,
        })
        if label is not UNSET:
            field_dict["label"] = label
        if delta is not UNSET:
            field_dict["delta"] = delta
        if evaluation is not UNSET:
            field_dict["evaluation"] = evaluation

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.graph_history_state import GraphHistoryState # noqa: PLC0415
        from ..models.stack_delta import StackDelta # noqa: PLC0415
        from ..models.stack_evaluation import StackEvaluation # noqa: PLC0415
        d = dict(src_dict)
        sequence = d.pop("sequence")

        kind = GraphEventKind(d.pop("kind"))




        revision = d.pop("revision")

        history = GraphHistoryState.from_dict(d.pop("history"))




        def _parse_label(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        label = _parse_label(d.pop("label", UNSET))


        def _parse_delta(data: object) -> None | StackDelta | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                delta_type_1 = StackDelta.from_dict(data)



                return delta_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackDelta | Unset, data)

        delta = _parse_delta(d.pop("delta", UNSET))


        def _parse_evaluation(data: object) -> None | StackEvaluation | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                evaluation_type_1 = StackEvaluation.from_dict(data)



                return evaluation_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackEvaluation | Unset, data)

        evaluation = _parse_evaluation(d.pop("evaluation", UNSET))


        stack_event = cls(
            sequence=sequence,
            kind=kind,
            revision=revision,
            history=history,
            label=label,
            delta=delta,
            evaluation=evaluation,
        )


        stack_event.additional_properties = d
        return stack_event

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
