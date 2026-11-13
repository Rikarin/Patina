from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from ...models.stack_channels_applied import StackChannelsApplied
from typing import cast



def _get_kwargs(
    id: str,

) -> dict[str, Any]:
    

    

    

    _kwargs: dict[str, Any] = {
        "method": "post",
        "url": "/api/v1/stacks/{id}/channels/apply-to-all".format(id=quote(str(id), safe=""),),
    }


    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> ProblemDetails | StackChannelsApplied | None:
    if response.status_code == 200:
        response_200 = StackChannelsApplied.from_dict(response.json())



        return response_200

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[ProblemDetails | StackChannelsApplied]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,

) -> Response[ProblemDetails | StackChannelsApplied]:
    """ Gives each of the model's other texture sets the channels this set has and it lacks (#141), each
    starting as what leaves its material alone. A set open in a session takes them as one undo step of
    its own, left unsaved; a set nobody has open has them written to its file. Nothing is taken off a
    set. Says what each set was given, or why it was left alone.

    Args:
        id (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | StackChannelsApplied]
     """


    kwargs = _get_kwargs(
        id=id,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    id: str,
    *,
    client: AuthenticatedClient | Client,

) -> ProblemDetails | StackChannelsApplied | None:
    """ Gives each of the model's other texture sets the channels this set has and it lacks (#141), each
    starting as what leaves its material alone. A set open in a session takes them as one undo step of
    its own, left unsaved; a set nobody has open has them written to its file. Nothing is taken off a
    set. Says what each set was given, or why it was left alone.

    Args:
        id (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | StackChannelsApplied
     """


    return sync_detailed(
        id=id,
client=client,

    ).parsed

async def asyncio_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,

) -> Response[ProblemDetails | StackChannelsApplied]:
    """ Gives each of the model's other texture sets the channels this set has and it lacks (#141), each
    starting as what leaves its material alone. A set open in a session takes them as one undo step of
    its own, left unsaved; a set nobody has open has them written to its file. Nothing is taken off a
    set. Says what each set was given, or why it was left alone.

    Args:
        id (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | StackChannelsApplied]
     """


    kwargs = _get_kwargs(
        id=id,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    id: str,
    *,
    client: AuthenticatedClient | Client,

) -> ProblemDetails | StackChannelsApplied | None:
    """ Gives each of the model's other texture sets the channels this set has and it lacks (#141), each
    starting as what leaves its material alone. A set open in a session takes them as one undo step of
    its own, left unsaved; a set nobody has open has them written to its file. Nothing is taken off a
    set. Says what each set was given, or why it was left alone.

    Args:
        id (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | StackChannelsApplied
     """


    return (await asyncio_detailed(
        id=id,
client=client,

    )).parsed
